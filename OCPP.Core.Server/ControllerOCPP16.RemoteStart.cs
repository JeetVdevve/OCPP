using Microsoft.Extensions.Logging;
using OCPP.Core.Server.Messages_OCPP16;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP16
    {
        public void HandleRemoteStart(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            Logger.LogInformation("Remote Start answer: ChargePointId={0} / MsgType={1} / ErrCode={2}", ChargePointStatus.Id, msgIn.MessageType, msgIn.ErrorCode);

            try
            {
                RemoteStartResponse remoteStartTransactionResponse = JsonConvert.DeserializeObject<RemoteStartResponse>(msgIn.JsonPayload);
                Logger.LogTrace("RemoteStartTransaction => Message deserialized");


                WriteMessageLog(ChargePointStatus?.Id, null, msgOut.Action, remoteStartTransactionResponse.Status.ToString(), msgIn.ErrorCode);

                if (msgOut.TaskCompletionSource != null)
                {
                    // Set API response as TaskCompletion-result
                    string apiResult = "{\"status\": " + JsonConvert.ToString(remoteStartTransactionResponse.Status.ToString()) + "}";
                    Logger.LogTrace("HandleReset => API response: {0}", apiResult);

                    msgOut.TaskCompletionSource.SetResult(apiResult);
                }


            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "HandleReset => Exception: {0}", exp.Message);
            }
        }
    }
}
