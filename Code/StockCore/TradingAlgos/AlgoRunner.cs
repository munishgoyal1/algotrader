using System;
using System.Collections.Generic;
using System.Threading;
//using HtmlParsingLibrary;
using StockTrader.Core;
using StockTrader.Platform.Logging;
using StockTrader.Utilities;

//using HttpLibrary;


namespace StockTrader.API.TradingAlgos
{
    public static class AlgoRunner
    {
        // Stock algo worker thread
        public static void Background_StockWorkerThread(object obj)
        {
            BrokerErrorCode errorCode = BrokerErrorCode.Success;
            BrokerSymbolAlgosObject stockTraderObj = (BrokerSymbolAlgosObject)obj;
            IBroker broker = stockTraderObj.Broker;
            //EquityStockTradeStats stockInfo = stockTraderObj.stockInfo;
            ITradingAlgo algo = stockTraderObj.Algos[0];

            // Thread local params
            int threadRunCount = 0;
            int threadFailCount = 0;

            //string stockCode = stockInfo.StockCode;
            string traceString;
            bool bChecked = false;
            // On program re-run, cancel any previous outstanding orders
            // BUGBUGCD: is this infinite loop necessary or try only once
            // and come out on failure
            do
            {
                errorCode = algo.Prolog();
                if (!errorCode.Equals(BrokerErrorCode.Success))
                    Thread.Sleep(1000 * 30);
            } while (!stockTraderObj.DoStopThread && !errorCode.Equals(BrokerErrorCode.Success));


            while (!stockTraderObj.DoStopThread)
            {
                // Main Algo
                errorCode = algo.RunCoreAlgo();

                // thread stats
                if (!errorCode.Equals(BrokerErrorCode.Success))
                    threadFailCount++;
                threadRunCount++;

                // Check remote control
                //ProgramRemoteControl remoteControlValue = CheckRemoteControlInAlgo(ref bChecked);

                //if (remoteControlValue.Equals(ProgramRemoteControl.STOP) ||
                //       remoteControlValue.Equals(ProgramRemoteControl.HIBERNATE))
                //    break;

                // Check if login problem
                if (errorCode == BrokerErrorCode.NotLoggedIn)
                {
                    FileTracing.TraceOut("AlgoRunner: " + errorCode.ToString() + " Trying to login again\n");
                    // MGOYAL: TEMP SLEEP this is for external login to do some work .
                    // External logger will have 2 minute (CheckAndLoginIfNeeded) window to complete a work before he is logged out.
                    // Sleep for a minute

                    // It means someone has stolen session, so let it be. if they need for more time then will set remote to PAUSE
                    Thread.Sleep(120000);
                    broker.LogOut();
                    errorCode = broker.CheckAndLogInIfNeeded(true);
                    continue;
                }
                if (errorCode == BrokerErrorCode.InvalidLoginPassword || errorCode == BrokerErrorCode.ChangePassword)
                {
                    FileTracing.TraceOut("AlgoRunner: InvalidLoginPassword, Exiting");
                    break;
                }
                Thread.Sleep(algo.GetSleepTimeInMilliSecs());
            }

            errorCode = algo.Epilog();

            // Trace out Thread run stats
            // TODO: more such fancy stuff later

            traceString = string.Format("AlgoRunner: Thread Run Stats: \nTotalRuns: {0}\nFailures: {1}\nFailurePercentagH: {2}",
                threadRunCount, threadFailCount, (double)(threadFailCount / (threadRunCount > 0 ? threadRunCount : 1)) * 100);

            FileTracing.TraceOut(traceString);
        }

        // Stock algo worker thread
        public static void BG_StockWorkerThread_SingleSymbol(object obj)
        {
            BrokerSymbolAlgosObject stockTraderObj = (BrokerSymbolAlgosObject)obj;
            IBroker broker = stockTraderObj.Broker;
            List<ITradingAlgo> algos = stockTraderObj.Algos;
            BrokerErrorCode errorCode = BrokerErrorCode.Success;
            EquitySymbolLiveTickGenerator ETicks = stockTraderObj.ETicks;
            DerivativeSymbolLiveTickGenerator DTicks = stockTraderObj.DTicks;
            bool isDayNewTicksStarted = false;
            bool isAnyAlgoSquareOffRetryPending = false;
            DateTime quoteTime;

            foreach (ITradingAlgo algo in algos)
            {
                algo.ErrorCode = algo.Prolog();
                if (algo.ErrorCode != BrokerErrorCode.Success)
                    errorCode = algo.ErrorCode;
            }

            do
            {
                errorCode = BrokerErrorCode.Success;

                foreach (ITradingAlgo algo in algos)
                    if (algo.ErrorCode != BrokerErrorCode.Success)
                    {
                        algo.ErrorCode = algo.Prolog();
                        if (algo.ErrorCode != BrokerErrorCode.Success)
                            errorCode = algo.ErrorCode;
                    }

                if (!errorCode.Equals(BrokerErrorCode.Success))
                    Thread.Sleep(1000 * 30);

            } while (!stockTraderObj.DoStopThread && !errorCode.Equals(BrokerErrorCode.Success));

            SymbolTick tick = new SymbolTick();
            tick.I = stockTraderObj.Instrument;

            while (!stockTraderObj.DoStopThread)
            {
                if (stockTraderObj.Instrument.InstrumentType == InstrumentType.Share)
                    tick.E = ETicks.GetTick(false, out errorCode);
                else
                    tick.D = DTicks.GetTick(false, out errorCode);

                if (!errorCode.Equals(BrokerErrorCode.Success))
                {
                    Thread.Sleep(1000);
                    continue;
                }

                if (stockTraderObj.Instrument.InstrumentType == InstrumentType.Share)
                    quoteTime = tick.E.Q[0].QuoteTime;
                else
                    quoteTime = tick.D.Q.QuoteTime;


                // to guard against previous day's ticks
                if (!isDayNewTicksStarted)
                {
                    isDayNewTicksStarted = quoteTime.Date == DateTime.Now.Date;

                    if (!isDayNewTicksStarted)
                    {
                        Thread.Sleep(10 * 1000);
                        continue;
                    }
                }

                // Main Algo
                foreach (ITradingAlgo algo in algos)
                    if (!algo.DoStopAlgo)
                    {
                        algo.IsExternallySuspended = false;
                        algo.ErrorCode = errorCode = algo.RunCoreAlgoLive(tick);
                        if (algo.IsOrderExecutionPending)
                            isAnyAlgoSquareOffRetryPending = true;
                    }
                    else algo.IsExternallySuspended = true;

                if (!isAnyAlgoSquareOffRetryPending)
                    Thread.Sleep(stockTraderObj.TickIntervalInMiliSeconds);//algo.GetSleepTimeInMilliSecs());

                isAnyAlgoSquareOffRetryPending = false;
            }

            foreach (ITradingAlgo algo in algos)
                algo.ErrorCode = algo.Epilog();

            if (stockTraderObj.Instrument.InstrumentType == InstrumentType.Share)
                ETicks.Close();
            else
                DTicks.Close();
        }

        // Not used currently
        public static ProgramRemoteControl CheckRemoteControlInAlgo(ref bool bChecked)
        {
            // TEMPTEMP: this check is currently temporary , need to place it properly to provide pause algo functionality
            // Check after every 5 minutes
            int minutes = DateTime.Now.Minute;
            int seconds = DateTime.Now.Second;
            string traceString;
            ProgramRemoteControl remoteControlValue = ProgramRemoteControl.RUN;
            if (minutes % 5 == 1)
            {
                bChecked = false;
            }
            if ((minutes >= 5) && (minutes % 5 == 0) && !bChecked)
            {
                bChecked = true;
                remoteControlValue = RemoteUtils.GetProgramRemoteControlValue();
                // keep looping until PAUSE status is reset
                while (remoteControlValue.Equals(ProgramRemoteControl.PAUSE))
                {
                    traceString = string.Format("AlgoRunner: Remote Control PAUSED the Algo, sleeping for 30 seconds before rechecking\n");
                    FileTracing.TraceOut(traceString);
                    // sleep 60 seconds
                    Thread.Sleep(60000);
                    remoteControlValue = RemoteUtils.GetProgramRemoteControlValue();
                }
                if (remoteControlValue.Equals(ProgramRemoteControl.STOP) ||
                    remoteControlValue.Equals(ProgramRemoteControl.HIBERNATE))
                {
                    traceString = string.Format("AlgoRunner: Remote Control issued STOP/HIBERNATE command to the Algo\n");
                    FileTracing.TraceOut(traceString);
                }
            }

            return remoteControlValue;
        }


    }
}