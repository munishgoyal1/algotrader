using System;

namespace StockTrader.Core
{
    // Trade book stock record info
    public class EquityBTSTTradeBookRecord
    {
        public DateTime Date;
        public string StockCode;
        public OrderDirection Direction;
        public int Quantity;
        public int MaxPermittedQty;
        public int BlockedQuantity;
        public int AvailableQuantity;
        public double Price;
        public double LTP;
        public double TradeValue;
        public double Brokerage;
        public string OrderRefenceNumber;
        public string SettlementNumber;
        public Exchange Exchange;
    }
}
