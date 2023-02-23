using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP16
    {
        public void HandleGetConfiguration(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            try
            {
                if (msgOut.TaskCompletionSource != null)
                {
                    // Set API response as TaskCompletion-result
                    string apiResult = $"{{\"status\": {msgIn.JsonPayload}}}";



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
