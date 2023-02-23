using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OCPP.Core.Database;
using OCPP.Core.Server;
using OCPP.Core.Server.Messages_OCPP16;

namespace OCPP.Core.Server
{
    public partial class OCPPMiddleware
    {
        /// <summary>
        /// Waits for new OCPP V1.6 messages on the open websocket connection and delegates processing to a controller
        /// </summary>
        private async Task Receive16(ChargePointStatus chargePointStatus, HttpContext context)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP16");
            ControllerOCPP16 controller16 = new ControllerOCPP16(_configuration, _logFactory, chargePointStatus);

            byte[] buffer = new byte[1024 * 4];
            MemoryStream memStream = new MemoryStream(buffer.Length);

            while (chargePointStatus.WebSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await chargePointStatus.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result != null && result.MessageType != WebSocketMessageType.Close)
                {
                    logger.LogTrace("OCPPMiddleware.Receive16 => Receiving segment: {0} bytes (EndOfMessage={1} / MsgType={2})", result.Count, result.EndOfMessage, result.MessageType);
                    memStream.Write(buffer, 0, result.Count);

                    if (result.EndOfMessage)
                    {
                        // read complete message into byte array
                        byte[] bMessage = memStream.ToArray();
                        // reset memory stream für next message
                        memStream = new MemoryStream(buffer.Length);

                        string dumpDir = _configuration.GetValue<string>("MessageDumpDir");
                        if (!string.IsNullOrWhiteSpace(dumpDir))
                        {
                            string path = Path.Combine(dumpDir, string.Format("{0}_ocpp16-in.txt", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-ffff")));
                            try
                            {
                                // Write incoming message into dump directory
                                File.WriteAllBytes(path, bMessage);
                            }
                            catch (Exception exp)
                            {
                                logger.LogError(exp, "OCPPMiddleware.Receive16 => Error dumping incoming message to path: '{0}'", path);
                            }
                        }

                        string ocppMessage = UTF8Encoding.UTF8.GetString(bMessage);

                        Match match = Regex.Match(ocppMessage, MessageRegExp);
                        if (match != null && match.Groups != null && match.Groups.Count >= 3)
                        {
                            string messageTypeId = match.Groups[1].Value;
                            string uniqueId = match.Groups[2].Value;
                            string action = match.Groups[3].Value;
                            string jsonPaylod = match.Groups[4].Value;
                            logger.LogInformation("OCPPMiddleware.Receive16 => OCPP-Message: Type={0} / ID={1} / Action={2})", messageTypeId, uniqueId, action);

                            OCPPMessage msgIn = new OCPPMessage(messageTypeId, uniqueId, action, jsonPaylod);
                            if (msgIn.MessageType == "2" || msgIn.MessageType == "1")
                            {
                                // Request from chargepoint to OCPP server
                                OCPPMessage msgOut = controller16.ProcessRequest(msgIn);

                                // Send OCPP message with optional logging/dump
                                await SendOcpp16Message(msgOut, logger, chargePointStatus.WebSocket);
                            }
                            else if (msgIn.MessageType == "3" || msgIn.MessageType == "4")
                            {
                                // Process answer from chargepoint
                                if (_requestQueue.ContainsKey(msgIn.UniqueId))
                                {
                                    controller16.ProcessAnswer(msgIn, _requestQueue[msgIn.UniqueId]);
                                    _requestQueue.Remove(msgIn.UniqueId);
                                }
                                else
                                {
                                    logger.LogError("OCPPMiddleware.Receive16 => HttpContext from caller not found / Msg: {0}", ocppMessage);
                                }
                            }
                            else
                            {
                                // Unknown message type
                                logger.LogError("OCPPMiddleware.Receive16 => Unknown message type: {0} / Msg: {1}", msgIn.MessageType, ocppMessage);
                            }
                        }
                        else
                        {
                            logger.LogWarning("OCPPMiddleware.Receive16 => Error in RegEx-Matching: Msg={0})", ocppMessage);
                        }
                    }
                }
                else
                {
                    logger.LogInformation("OCPPMiddleware.Receive16 => WebSocket Closed: CloseStatus={0} / MessageType={1}", result?.CloseStatus, result?.MessageType);
                    await chargePointStatus.WebSocket.CloseOutputAsync((WebSocketCloseStatus)3001, string.Empty, CancellationToken.None);
                }
            }
            logger.LogInformation("OCPPMiddleware.Receive16 => Websocket closed: State={0} / CloseStatus={1}", chargePointStatus.WebSocket.State, chargePointStatus.WebSocket.CloseStatus);
            ChargePointStatus dummy;
            _chargePointStatusDict.Remove(chargePointStatus.Id, out dummy);
        }

        /// <summary>
        /// Waits for new OCPP V1.6 messages on the open websocket connection and delegates processing to a controller
        /// </summary>
        private async Task Reset16(ChargePointStatus chargePointStatus, HttpContext apiCallerContext)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP16");
            ControllerOCPP16 controller16 = new ControllerOCPP16(_configuration, _logFactory, chargePointStatus);

            Messages_OCPP16.ResetRequest resetRequest = new Messages_OCPP16.ResetRequest();
            resetRequest.Type = Messages_OCPP16.ResetRequestType.Soft;
            string jsonResetRequest = JsonConvert.SerializeObject(resetRequest);

            OCPPMessage msgOut = new OCPPMessage();
            msgOut.MessageType = "2";
            msgOut.Action = "Reset";
            msgOut.UniqueId = Guid.NewGuid().ToString("N");
            msgOut.JsonPayload = jsonResetRequest;
            msgOut.TaskCompletionSource = new TaskCompletionSource<string>();

            // store HttpContext with MsgId for later answer processing (=> send anwer to API caller)
            _requestQueue.Add(msgOut.UniqueId, msgOut);

            // Send OCPP message with optional logging/dump
            await SendOcpp16Message(msgOut, logger, chargePointStatus.WebSocket);

            // Wait for asynchronous chargepoint response and processing
            string apiResult = await msgOut.TaskCompletionSource.Task;

            // 
            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        /// <summary>
        /// Sends a Unlock-Request to the chargepoint
        /// </summary>
        private async Task UnlockConnector16(ChargePointStatus chargePointStatus, HttpContext apiCallerContext)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP16");
            ControllerOCPP16 controller16 = new ControllerOCPP16(_configuration, _logFactory, chargePointStatus);

            Messages_OCPP16.UnlockConnectorRequest unlockConnectorRequest = new Messages_OCPP16.UnlockConnectorRequest();
            unlockConnectorRequest.ConnectorId = 0;

            string jsonResetRequest = JsonConvert.SerializeObject(unlockConnectorRequest);

            OCPPMessage msgOut = new OCPPMessage();
            msgOut.MessageType = "2";
            msgOut.Action = "UnlockConnector";
            msgOut.UniqueId = Guid.NewGuid().ToString("N");
            msgOut.JsonPayload = jsonResetRequest;
            msgOut.TaskCompletionSource = new TaskCompletionSource<string>();

            // store HttpContext with MsgId for later answer processing (=> send anwer to API caller)
            _requestQueue.Add(msgOut.UniqueId, msgOut);

            // Send OCPP message with optional logging/dump
            await SendOcpp16Message(msgOut, logger, chargePointStatus.WebSocket);

            // Wait for asynchronous chargepoint response and processing
            string apiResult = await msgOut.TaskCompletionSource.Task;

            // 
            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        private async Task RemoteStartTransaction16(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, int connectorId, String idTag)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP16");
            ControllerOCPP16 controller16 = new ControllerOCPP16(_configuration, _logFactory, chargePointStatus);

            Messages_OCPP16.RemoteStartRequest resetRequest = new Messages_OCPP16.RemoteStartRequest();
            resetRequest.ConnectorId = connectorId;
            // change this later
            resetRequest.IdTag = idTag;
            string jsonResetRequest = JsonConvert.SerializeObject(resetRequest);

            OCPPMessage msgOut = new OCPPMessage();
            msgOut.MessageType = "2";
            msgOut.Action = "RemoteStartTransaction";
            msgOut.UniqueId = Guid.NewGuid().ToString("N");
            msgOut.JsonPayload = jsonResetRequest;
            msgOut.TaskCompletionSource = new TaskCompletionSource<string>();

            // store HttpContext with MsgId for later answer processing (=> send anwer to API caller)
            _requestQueue.Add(msgOut.UniqueId, msgOut);

            // Send OCPP message with optional logging/dump
            await SendOcpp16Message(msgOut, logger, chargePointStatus.WebSocket);

            // Wait for asynchronous chargepoint response and processing
            string apiResult = await msgOut.TaskCompletionSource.Task;

            // 
            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        private async Task RemoteStopTransaction16(ChargePointStatus chargePointStatus, HttpContext apiCallerContext)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP16");
            ControllerOCPP16 controller16 = new ControllerOCPP16(_configuration, _logFactory, chargePointStatus);

            Messages_OCPP16.RemoteStopRequest stopRequest = new Messages_OCPP16.RemoteStopRequest();

            Transaction transaction = controller16.getTransactions(chargePointStatus);

            stopRequest.TransactionId = transaction.TransactionId;
            // change this later
            string jsonResetRequest = JsonConvert.SerializeObject(stopRequest);

            OCPPMessage msgOut = new OCPPMessage();
            msgOut.MessageType = "2";
            msgOut.Action = "RemoteStopTransaction";
            msgOut.UniqueId = Guid.NewGuid().ToString("N");
            msgOut.JsonPayload = jsonResetRequest;
            msgOut.TaskCompletionSource = new TaskCompletionSource<string>();

            // store HttpContext with MsgId for later answer processing (=> send anwer to API caller)
            _requestQueue.Add(msgOut.UniqueId, msgOut);

            // Send OCPP message with optional logging/dump
            await SendOcpp16Message(msgOut, logger, chargePointStatus.WebSocket);

            // Wait for asynchronous chargepoint response and processing
            string apiResult = await msgOut.TaskCompletionSource.Task;

            // 
            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        private async Task GetConfiguration(ChargePointStatus chargePointStatus, HttpContext apiCallerContext)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP16");
            ControllerOCPP16 controller16 = new ControllerOCPP16(_configuration, _logFactory, chargePointStatus);

            MeterValuesResponse meterValuesResponse = new MeterValuesResponse();

            OCPPMessage msgOut = new OCPPMessage();
            msgOut.MessageType = "2";
            msgOut.Action = "GetConfiguration";
            msgOut.UniqueId = Guid.NewGuid().ToString("N");
            msgOut.JsonPayload = JsonConvert.SerializeObject(meterValuesResponse);
            msgOut.TaskCompletionSource = new TaskCompletionSource<string>();

            // store HttpContext with MsgId for later answer processing (=> send anwer to API caller)
            _requestQueue.Add(msgOut.UniqueId, msgOut);

            // Send OCPP message with optional logging/dump
            await SendOcpp16Message(msgOut, logger, chargePointStatus.WebSocket);

            // Wait for asynchronous chargepoint response and processing
            string apiResult = await msgOut.TaskCompletionSource.Task;

            // 
            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        private async Task SetConfiguration(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, string[] parameterArray)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP16");
            ControllerOCPP16 controller16 = new ControllerOCPP16(_configuration, _logFactory, chargePointStatus);

            SetConfigurationRequest setConfig =  setConfigRequests(parameterArray);

            OCPPMessage msgOut = new OCPPMessage();
            msgOut.MessageType = "2";
            msgOut.Action = "ChangeConfiguration";
            msgOut.UniqueId = Guid.NewGuid().ToString("N");
            msgOut.JsonPayload = JsonConvert.SerializeObject(setConfig);
            msgOut.TaskCompletionSource = new TaskCompletionSource<string>();

            // store HttpContext with MsgId for later answer processing (=> send anwer to API caller)
            _requestQueue.Add(msgOut.UniqueId, msgOut);

            // Send OCPP message with optional logging/dump
            await SendOcpp16Message(msgOut, logger, chargePointStatus.WebSocket);

            // Wait for asynchronous chargepoint response and processing
            string apiResult = await msgOut.TaskCompletionSource.Task;

            // 
            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        private async Task SetChargingProfile(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, string[] parametersArray)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP16");
            ControllerOCPP16 controller16 = new ControllerOCPP16(_configuration, _logFactory, chargePointStatus);

            /* dummy data */

            List<ChargingSchedulePeriod> schedulePeriod = setChargingSchedulePeriod(parametersArray);

            ChargingSchedule chargingSchedule = new ChargingSchedule();
            chargingSchedule.ChargingRateUnit = ChargingRateUnitType.A;
            chargingSchedule.ChargingSchedulePeriod = schedulePeriod;

            ChargingProfile chargingProfile = new ChargingProfile();
            chargingProfile.ChargingProfileId = 1;
            chargingProfile.StackLevel = 0;
            chargingProfile.ChargingProfilePurpose = chargingProfilePurposeType.TxDefaultProfile;
            chargingProfile.ChargingProfileKindType = chargingProfileKindType.Absolute;
            chargingProfile.ChargingSchedule = chargingSchedule;

            SetChargingProfileRequest setChargingProfileRequest = setChargingProfile(parametersArray);
            setChargingProfileRequest.ChargingProfile = chargingProfile;
            /**/


            OCPPMessage msgOut = new OCPPMessage();
            msgOut.MessageType = "2";
            msgOut.Action = "SetChargingProfile";
            msgOut.UniqueId = Guid.NewGuid().ToString("N");
            msgOut.JsonPayload = JsonConvert.SerializeObject(setChargingProfileRequest);
            msgOut.TaskCompletionSource = new TaskCompletionSource<string>();

            // store HttpContext with MsgId for later answer processing (=> send anwer to API caller)
            _requestQueue.Add(msgOut.UniqueId, msgOut);

            // Send OCPP message with optional logging/dump
            await SendOcpp16Message(msgOut, logger, chargePointStatus.WebSocket);

            // Wait for asynchronous chargepoint response and processing
            string apiResult = await msgOut.TaskCompletionSource.Task;

            // 
            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }
        
        private async Task GetCompositeSchedule(ChargePointStatus chargePointStatus, HttpContext apiCallerContext)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP16");
            ControllerOCPP16 controller16 = new ControllerOCPP16(_configuration, _logFactory, chargePointStatus);

            GetCompositeScheduleRequest getCompositeScheduleRequest = new GetCompositeScheduleRequest();
            getCompositeScheduleRequest.ConnectorId = 1;
            getCompositeScheduleRequest.Duration = 1;

            OCPPMessage msgOut = new OCPPMessage();
            msgOut.MessageType = "2";
            msgOut.Action = "GetCompositeSchedule";
            msgOut.UniqueId = Guid.NewGuid().ToString("N");
            msgOut.JsonPayload = JsonConvert.SerializeObject(getCompositeScheduleRequest);
            msgOut.TaskCompletionSource = new TaskCompletionSource<string>();

            // store HttpContext with MsgId for later answer processing (=> send anwer to API caller)
            _requestQueue.Add(msgOut.UniqueId, msgOut);

            // Send OCPP message with optional logging/dump
            await SendOcpp16Message(msgOut, logger, chargePointStatus.WebSocket);

            // Wait for asynchronous chargepoint response and processing
            string apiResult = await msgOut.TaskCompletionSource.Task;

            // 
            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);

        }


        private async Task SendOcpp16Message(OCPPMessage msg, ILogger logger, WebSocket webSocket)
        {
            string ocppTextMessage = null;

            if (string.IsNullOrEmpty(msg.ErrorCode))
            {
                if (msg.MessageType == "2")
                {
                    // OCPP-Request
                    ocppTextMessage = string.Format("[{0},\"{1}\",\"{2}\",{3}]", msg.MessageType, msg.UniqueId, msg.Action, msg.JsonPayload);
                }
                else
                {
                    // OCPP-Response
                    ocppTextMessage = string.Format("[{0},\"{1}\",{2}]", msg.MessageType, msg.UniqueId, msg.JsonPayload);
                }
            }
            else
            {
                ocppTextMessage = string.Format("[{0},\"{1}\",\"{2}\",\"{3}\",{4}]", msg.MessageType, msg.UniqueId, msg.ErrorCode, msg.ErrorDescription, "{}");
            }
            logger.LogTrace("OCPPMiddleware.OCPP16 => SendOcppMessage: {0}", ocppTextMessage);

            if (string.IsNullOrEmpty(ocppTextMessage))
            {
                // invalid message
                ocppTextMessage = string.Format("[{0},\"{1}\",\"{2}\",\"{3}\",{4}]", "4", string.Empty, Messages_OCPP16.ErrorCodes.ProtocolError, string.Empty, "{}");
            }

            string dumpDir = _configuration.GetValue<string>("MessageDumpDir");
            if (!string.IsNullOrWhiteSpace(dumpDir))
            {
                // Write outgoing message into dump directory
                string path = Path.Combine(dumpDir, string.Format("{0}_ocpp16-out.txt", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-ffff")));
                try
                {
                    File.WriteAllText(path, ocppTextMessage);
                }
                catch (Exception exp)
                {
                    logger.LogError(exp, "OCPPMiddleware.SendOcpp16Message=> Error dumping message to path: '{0}'", path);
                }
            }

            byte[] binaryMessage = UTF8Encoding.UTF8.GetBytes(ocppTextMessage);
            await webSocket.SendAsync(new ArraySegment<byte>(binaryMessage, 0, binaryMessage.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    
        private SetConfigurationRequest setConfigRequests(String[] parameterArray)
        {
            SetConfigurationRequest  setConfig= new Messages_OCPP16.SetConfigurationRequest();

            foreach(var element in parameterArray)
            {
                string[] valueKeyPair = element.Split("=");
                switch(valueKeyPair[0])
                {
                    case "ChargeRate":
                        setConfig.key = valueKeyPair[0];
                        setConfig.value = valueKeyPair[1];
                        break;
                    default:
                        break;
                }
                   
            }

            return setConfig;
        }

        // this creates an object for the setChargingProfile and sets all the parameters 
        private List<ChargingSchedulePeriod> setChargingSchedulePeriod(String[] parameterArray)
        {
            List<ChargingSchedulePeriod> chargingSchedulePeriod = new List<Messages_OCPP16.ChargingSchedulePeriod>();


            foreach (var element in parameterArray)
            {
                string[] valueKeyPair = element.Split("=");
                switch (valueKeyPair[0])
                {
                    case "current":
                        ChargingSchedulePeriod chargeSched = new ChargingSchedulePeriod();
                        chargeSched.limit = int.Parse(valueKeyPair[1]);
                        chargeSched.startPeriod = 0;
                        chargingSchedulePeriod.Add(chargeSched);
                        break;
                    default:
                        break;
                }

            }

            return chargingSchedulePeriod;
        }

        // this creates an object for the setChargingProfile and sets all the parameters 
        private SetChargingProfileRequest setChargingProfile(String[] parameterArray)
        {
            SetChargingProfileRequest setChargingProfile = new Messages_OCPP16.SetChargingProfileRequest();

                foreach (var element in parameterArray)
                {
                    string[] valueKeyPair = element.Split("=");
                    switch (valueKeyPair[0])
                    {
                        case "connectorId":
                            setChargingProfile.ConnectorId = int.Parse(valueKeyPair[1]);
                            break;
                        default:
                            break;
                    }

                }

                return setChargingProfile;
        }

    }
}
