using System;
using System.Collections.Generic;
using StockTrader.Core;
using StockTrader.Platform.Logging;

namespace StockTrader.Broker.IciciDirect
{
    public class IciciConstants
    { 
    // String codes corresponding to OrderStatus enum 
        public static string[] OrderStatusString =
        {
        //     QUEUED,
        //REQUESTED,
        //ORDERED,
        //PARTEXEC,
        //EXECUTED,
        //EXPIRED,
        //REJECTED,
        //CANCELLED,
        //PARTEXECANDCANCELLED,
        //NOTFOUND
            "Q",
            "R",
            "O",
            "P",
            "E",
            "X",
            "J",
            "C",
            "C", // Not sure about correctness of this code
            "N"  // Not sure about correctness of this code
        };
        // String codes corresponding to Exchange enum 
        public static string[] ExchangeString =
        {
            "NSE",
            "BSE"
        };
        // String codes corresponding to OrderDirection enum 
        public static string[] OrderDirectionString =
        {
            "B",
            "S"
        };
    }
}