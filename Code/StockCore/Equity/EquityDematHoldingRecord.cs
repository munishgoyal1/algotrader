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

    //EXCHANGE,PRODUCT,SYMBOL,TOKEN,BUY_AMOUNT,SELL_AMOUNT,BUY_QUANTITY,SELL_QUANTITY,CF_BUY_AMOUNT,CF_SELL_AMOUNT,CF_BUY_QUANTITY,CF_SELL_QUANTITY,
    //AVG_BUY_PRICE,AVG_SELL_PRICE,NET_QUANTITY,CLOSE_PRICE,LAST_TRADED_PRICE,REALIZED_PROFIT,UNREALIZED_PROFIT,CF_AVG_PRICE
//NSE_EQ, I, FEDERALBNK,1023,81.85,0,1,0,0,0,0,0,81.85,0,1,81.85,82.4,0,0.550000000000011,0

    // Demat allocation stock record info
    public class EquityPositionRecord
    {
        public string Exchange;
        public string StockCode;
        public EquityOrderType EquityOrderType;
        public int NetQuantity;
    }
}
