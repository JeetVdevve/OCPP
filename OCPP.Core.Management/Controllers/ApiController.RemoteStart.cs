using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Controllers
{
    public partial class ApiController : BaseController
    {

        [Authorize]
        [HttpPost]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> RemoteStartTransaction(string Id, string connectorId, string idTag)
        {
            if (User != null && !User.IsInRole(Constants.AdminRoleName))
            {
                Logger.LogWarning("UnlockConnector: Request by non-administrator: {0}", User?.Identity?.Name);
                return StatusCode((int)HttpStatusCode.Unauthorized);
                return StatusCode((int)HttpStatusCode.Unauthorized);
            }

            int httpStatuscode = (int)HttpStatusCode.OK;
            string resultContent = string.Empty;

            if (!string.IsNullOrEmpty(Id))
            {
                try
                {
                    using (OCPPCoreContext dbContext = new OCPPCoreContext(this.Config))
                    {

                        ChargePoint chargePoint = dbContext.ChargePoints.Find(Id);
                        if (chargePoint != null)
                        {
                            string serverApiUrl = base.Config.GetValue<string>("ServerApiUrl");
                            string apiKeyConfig = base.Config.GetValue<string>("ApiKey");
                            if (!string.IsNullOrEmpty(serverApiUrl))
                            {
                                try
                                {
                                    using (var httpClient = new HttpClient())
                                    {
                                        if (!serverApiUrl.EndsWith('/'))
                                        {
                                            serverApiUrl += "/";
                                        }
                                        Uri uri = new Uri(serverApiUrl);
                                        uri = new Uri(uri, $"RemoteStartTransaction/{Uri.EscapeUriString(Id)}? idTag={idTag}&connectorId={connectorId}");
                                        httpClient.Timeout = new TimeSpan(0, 0, 4); // use short timeout

                                        // API-Key authentication?
                                        if (!string.IsNullOrWhiteSpace(apiKeyConfig))
                                        {
                                            httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKeyConfig);
                                        }
                                        else
                                        {
                                            Logger.LogWarning("RemoteStartConnector: No API-Key configured!");
                                        }

                                        HttpResponseMessage response = await httpClient.GetAsync(uri);
                                        if (response.StatusCode == HttpStatusCode.OK)
                                        {
                                            string jsonResult = await response.Content.ReadAsStringAsync();
                                            if (!string.IsNullOrEmpty(jsonResult))
                                            {
                                                try
                                                {
                                                    /*dynamic jsonObject = JsonConvert.DeserializeObject(jsonResult);
                                                    Logger.LogInformation("UnlockConnector: Result of API request is '{0}'", jsonResult);
                                                    string status = jsonObject.status;
                                                    switch (status)
                                                    {
                                                        case "Unlocked":
                                                            resultContent = _localizer["UnlockConnectorAccepted"];
                                                            break;
                                                        case "UnlockFailed":
                                                        case "OngoingAuthorizedTransaction":
                                                        case "UnknownConnector":
                                                            resultContent = _localizer["UnlockConnectorFailed"];
                                                            break;
                                                        case "NotSupported":
                                                            resultContent = _localizer["UnlockConnectorNotSupported"];
                                                            break;
                                                        default:
                                                            resultContent = string.Format(_localizer["UnlockConnectorUnknownStatus"], status);
                                                            break;
                                                    }*/
                                                }
                                                catch (Exception exp)
                                                {
                                                    Logger.LogError(exp, "UnlockConnector: Error in JSON result => {0}", exp.Message);
                                                    httpStatuscode = (int)HttpStatusCode.OK;
                                                    resultContent = _localizer["UnlockConnectorError"];
                                                }
                                            }
                                            else
                                            {
                                                Logger.LogError("RemoteStartConnector: Result of API request is empty");
                                                httpStatuscode = (int)HttpStatusCode.OK;
                                                resultContent = _localizer["UnlockConnectorError"];
                                            }
                                        }
                                        else if (response.StatusCode == HttpStatusCode.NotFound)
                                        {
                                            // Chargepoint offline
                                            httpStatuscode = (int)HttpStatusCode.OK;
                                            resultContent = _localizer["UnlockConnectorOffline"];
                                        }
                                        else
                                        {
                                            Logger.LogError("RemoteStartConnector: Result of API  request => httpStatus={0}", response.StatusCode);
                                            httpStatuscode = (int)HttpStatusCode.OK;
                                            resultContent = _localizer["UnlockConnectorError"];
                                        }
                                    }
                                }
                                catch (Exception exp)
                                {
                                    Logger.LogError(exp, "RemoteStartConnector: Error in API request => {0}", exp.Message);
                                    httpStatuscode = (int)HttpStatusCode.OK;
                                    resultContent = _localizer["UnlockConnectorError"];
                                }
                            }
                        }
                    }
                } catch(Exception error)
                {

                }
            }

            return StatusCode(httpStatuscode, resultContent);
        }
    }
}
