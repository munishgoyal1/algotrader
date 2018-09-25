using System;

namespace StockTrader.Core
{
    // Trade book stock record info
    public class EquityTradeBookRecord
    {
        public DateTime DateTime;
        public string StockCode;
        public OrderDirection Direction;
        public EquityOrderType EquityOrderType;
        public int Quantity;
        public int NewQuantity;
        public double Price;
        public double TradeValue;
        public double Brokerage;
        public string OrderId;
        public string TradeId;
        public string SettlementNumber;
        public string Exchange;
    }
}
