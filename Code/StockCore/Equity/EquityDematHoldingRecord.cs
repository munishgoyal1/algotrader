using System;

namespace StockTrader.Core
{
    // Demat allocation stock record info
    public class EquityDematHoldingRecord
    {
        public DateTime Date;
        public string StockCode;
        public OrderDirection Direction;
        public int Quantity;
        public int BlockedQuantity;
        public int AvailableQuantity;
        public double Price;
        public double LTP;
        public double TradeValue;
        public double Brokerage;
        public string OrderRefenceNumber;
        public string SettlementNumber;
        public string Exchange;
    }
}
