using System;
using System.Collections.Generic;
using System.Threading;
using StockTrader.Config;
using StockTrader.Core;
using StockTrader.Platform.Logging;

namespace StockTrader.Utilities.Broker
{
    // Broker Account object passed into per-BrokerAccount login thread
    public class BrokingAccountObject
    {
        public BrokingAccountObject(IBroker account, object customData = null, object customData2 = null)
        {
            Broker = account;
            DoStopThread = false;
            CustomData = customData;
            CustomData2 = customData2;
        }

        public IBroker Broker;
        public object CustomData;
        public object CustomData2;
        public bool DoStopThread;
    }

    // Per-Account thread object
    public struct BrokingAccountThread
    {
        public Thread thread;
        public BrokingAccountObject brokerAccountObj;
    }

    public class LoginUtils
    {
        // Login worker thread
        public static void Background_LoginCheckerThread(object obj)
        {
            BrokingAccountObject brokerAccountObj = (BrokingAccountObject)obj;
            IBroker iciciDirectAccount = brokerAccountObj.Broker;
            BrokerErrorCode errorCode = BrokerErrorCode.Success;

            while (!brokerAccountObj.DoStopThread)
            {
                // SIMPLETODO uncomment the below line
                errorCode = iciciDirectAccount.CheckAndLogInIfNeeded(false);
                string traceString = "Periodic login check: " + errorCode.ToString();
                FileTracing.TraceOut(traceString);

                // sleep of 3 minutes
                Thread.Sleep(3000 * 60);
            }
        }

     
     }
}
