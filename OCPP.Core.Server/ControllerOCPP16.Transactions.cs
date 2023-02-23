using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP16
    {
        public Transaction getTransactions(ChargePointStatus chargePointStatus)
        {
            try
            {
                OCPPCoreContext dbContext = new OCPPCoreContext(Configuration);

                Transaction transaction = dbContext.Transactions
                                       .Where(t => t.ChargePointId == chargePointStatus.Id)
                                       .OrderByDescending(t => t.TransactionId)
                                       .FirstOrDefault();

                if (transaction != null)
                {
                    Logger.LogTrace("StopTransaction => Last transaction id={0} / Start='{1}' / Stop='{2}'", transaction.TransactionId, transaction.StartTime.ToString("O"), transaction?.StopTime?.ToString("o"));
                    if (transaction.StopTime.HasValue)
                    {
                        Logger.LogTrace("StopTransaction => Last transaction (id={0}) is already closed ", transaction.TransactionId);
                        transaction = null;
                    }
                }
                else
                {
                    Logger.LogTrace("StopTransaction => Found no transaction for charge point '{0}'", ChargePointStatus.Id);
                }

                return transaction;
            } catch (Exception e)
            {
                return null;
            }

        }
    }
}
