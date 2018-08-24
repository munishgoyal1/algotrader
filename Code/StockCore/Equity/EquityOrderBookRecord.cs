using System;
using System.Collections;
using System.Data;

namespace StockTrader.Core
{
    // Order book stock record info
    public class EquityOrderBookRecord
    {
        public DateTime DateTime;
        public string StockCode;
        public OrderDirection Direction;
        public int Quantity;
        public double Price;
        public string OrderId;
        public string Exchange;
        public OrderStatus Status;
        public int OpenQty;
        public int ExecutedQty;
        public int ExpiredQty;
        public int CancelledQty;
        public double StopLossPrice;
    }
}
