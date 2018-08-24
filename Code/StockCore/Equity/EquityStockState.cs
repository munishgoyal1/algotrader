using System;
using System.Collections.Generic;
using StockTrader.Utilities;


namespace StockTrader.Core
{
    public static class EquityStockState
    {
        ////////////////////////////////////
        //////      EQUITY STOCK STATE      //////
        //////////////////////////////////    


        public static BrokerErrorCode EquityRefreshStockStateToday(IBroker broker, EquityStockTradeStats stockInfo)
        {
            DateTime EarliestValidMarketOpenDate = MarketUtils.GetMarketCurrentDate();
            return EquityRefreshStockState(broker, EarliestValidMarketOpenDate, EarliestValidMarketOpenDate, stockInfo);
        }

        private static BrokerErrorCode EquityRefreshStockState(
            IBroker broker,
            DateTime fromDate,
            DateTime toDate,
            EquityStockTradeStats stockInfo)
        {
            BrokerErrorCode errorCode = BrokerErrorCode.Unknown;

            if (stockInfo == null)
            {
                return BrokerErrorCode.InValidArg;
            }
            Dictionary<string, EquityOrderBookRecord> orders = new Dictionary<string, EquityOrderBookRecord>();
            Dictionary<string, EquityTradeBookRecord> trades = new Dictionary<string, EquityTradeBookRecord>();

            // Get stock filtered Order book
            errorCode = broker.GetEquityOrderBook(fromDate, toDate, false, true, stockInfo.StockCode, out orders);

            if (errorCode == BrokerErrorCode.Success)
            {
                // Get stock filtered Trade book
                errorCode = broker.GetEquityTradeBook(fromDate, toDate, false, stockInfo.StockCode, out trades);
            }

            if (errorCode == BrokerErrorCode.Success)
            {
                // Call stock refresh method to update its state
                stockInfo.RefreshState(orders, trades);
            }

            return errorCode;
        }
    }

}