using System;
using System.Collections.Generic;
using System.Threading;
using StockTrader.Core;
using StockTrader.Platform.Logging;

namespace StockTrader.Utilities.Broker
{
    public class BrokerUtils
    {

        // Helper function to cancel stock specific all outstanding orders and trace it out
        public static BrokerErrorCode CancelStockOutstandingOrdersAndTrace(IBroker broker, string stockCode)
        {
            Dictionary<string, BrokerErrorCode> cancelOrderErrCodes;
            BrokerErrorCode errorCode = broker.CancelAllOutstandingEquityOrders(stockCode, out cancelOrderErrCodes);
            string traceString = Thread.CurrentThread.Name + ": CancelStockAllOutstandingEquityOrders: " + errorCode.ToString() + "\n";
            foreach (KeyValuePair<string, BrokerErrorCode> errCodePair in cancelOrderErrCodes)
            {
                traceString += "Order: " + errCodePair.Key + ": Cancel status code is " + "\"" + errCodePair.Value.ToString() + "\"\n";
            }
            FileTracing.TraceOut(traceString);

            return errorCode;
        }

        public static bool IsErrorFatal(BrokerErrorCode errorCode)
        {
            if (errorCode == BrokerErrorCode.NullResponse || errorCode == BrokerErrorCode.OrderCancelFailed ||
                errorCode == BrokerErrorCode.Http || errorCode == BrokerErrorCode.ServerError ||
                errorCode == BrokerErrorCode.TechnicalReason ||
                errorCode == BrokerErrorCode.Success)
            {
                return false;
            }

            return true;
        }

    }
}
