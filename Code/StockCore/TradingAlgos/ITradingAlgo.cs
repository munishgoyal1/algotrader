using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using StockTrader.Common;
using StockTrader.Core;
using StockTrader.Platform.Logging;
using StockTrader.Utilities;
using StockTrader.Utilities.Broker;

// MD = MoreDebug
// DB = DataBase
// MC = MarketClosing
// SO = SquareOff
// EOD = End of Day
// EOP = End of Period

// SOMC-DB = SquareOff on MarketClosing, Runs on IntraDay Tick data and maintains EOD trading stats (profit/loss, num trades etc.) incl. EOP stats

namespace StockTrader.API.TradingAlgos
{
    // Any stock trading algo's generic interface
    public interface ITradingAlgo
    {
        BrokerErrorCode RunCoreAlgo();
        BrokerErrorCode RunCoreAlgoLive(SymbolTick si);
        BrokerErrorCode Prolog();
        BrokerErrorCode Epilog();
        int GetSleepTimeInMilliSecs();
        BrokerErrorCode ErrorCode { get; set; }
        bool DoStopAlgo { get; set; }
        AlgoOrderPlaceState AlgoWorkingState { get; set; }
        bool IsExternallySuspended { get; set; }
        bool IsOrderExecutionPending { get; set; }
        string Description();
    }
}
