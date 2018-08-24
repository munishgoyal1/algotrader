using System;
using System.Collections.Generic;

namespace StockTrader.Core
{
    public interface IBroker : IDisposable
    {
        // Login methods
        BrokerErrorCode LogIn();
        void LogOut();
        BrokerErrorCode IsLoggedIn();
        BrokerErrorCode CheckAndLogInIfNeeded(bool bForceLogin, bool bCheckRemoteControl = false);

        // Equity methods
        BrokerErrorCode GetEquityQuote(string stockCode, out EquitySymbolQuote[] info);
        BrokerErrorCode GetEquitySpread(string stockCode, out EquitySymbolSpread[] info);

        BrokerErrorCode PlaceEquityMarginDeliveryFBSOrder(string stockCode,
            int quantity,
            string price,
            OrderPriceType orderPriceType,
            OrderDirection orderDirection,
            EquityOrderType orderType,
            Exchange exchange,
            out string orderRef);

        BrokerErrorCode ConvertToDeliveryFromPendingForDelivery(string stockCode,
            int openQty,
            int toConvertQty,
            string settlementRef,
            Exchange exchange);

        BrokerErrorCode GetEquityTradeBookToday(bool newTradesOnly,
                string stockCode,
                out Dictionary<string, EquityTradeBookRecord> trades);

        BrokerErrorCode GetEquityOrderBookToday(bool newTradesOnly,
        bool bOnlyOutstandingOrders,
        string stockCode,
        out Dictionary<string, EquityOrderBookRecord> orders);

        BrokerErrorCode CancelEquityOrder(string orderRef);
        
        BrokerErrorCode CancelAllOutstandingEquityOrders(string stockCode,
            out Dictionary<string, BrokerErrorCode> cancelOrderErrCodes);

    }
}
