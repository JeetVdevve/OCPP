using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Server.Messages_OCPP16;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP16
    {
        public void HandleSetConfiguration(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            try
            {
                SetConfigurationResponse setConfigResponse = JsonConvert.DeserializeObject<SetConfigurationResponse>(msgIn.JsonPayload);
                Logger.LogTrace("SetConfig => Message deserialized");


                WriteMessageLog(ChargePointStatus?.Id, null, msgOut.Action, setConfigResponse.Status.ToString(), msgIn.ErrorCode);

                if (msgOut.TaskCompletionSource != null)
                {
                    // Set API response as TaskCompletion-result
                    string apiResult = "{\"status\": " + JsonConvert.ToString(setConfigResponse.Status.ToString()) + "}";
                    Logger.LogTrace("SetConfig => API response: {0}", apiResult);

                    msgOut.TaskCompletionSource.SetResult(apiResult);
                }


            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "SetConfig => Exception: {0}", exp.Message);
            }
        }
    }
}
