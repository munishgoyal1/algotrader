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
        BrokerErrorCode PlaceEquityDeliveryBTSTOrder(string stockCode,
            int quantity,
            string price,
            OrderPriceType orderPriceType,
            OrderDirection orderDirection,
            EquityOrderType orderType,
            Exchange exchange,
            string settlementNumber,
            out string orderRef);

        BrokerErrorCode PlaceEquityMarginDeliveryFBSOrder(string stockCode,
            int quantity,
            string price,
            OrderPriceType orderPriceType,
            OrderDirection orderDirection,
            EquityOrderType orderType,
            Exchange exchange,
            out string orderRef);

        BrokerErrorCode PlaceEquityMarginSquareOffOrder(string stockCode,
            int availableQty,
            int quantity,
            string price,
            OrderPriceType orderPriceType,
            OrderDirection orderDirection,
            EquityOrderType orderType,
            string settlementRef,
            Exchange exchange,
            out string orderRef);

        BrokerErrorCode ConvertToDeliveryFromMarginOpenPositions(string stockCode,
            int openQty,
            int toConvertQty,
            string settlementRef,
            OrderDirection ordDirection,
            Exchange exchange);

        BrokerErrorCode ConvertToDeliveryFromPendingForDelivery(string stockCode,
            int openQty,
            int toConvertQty,
            string settlementRef,
            Exchange exchange);

        BrokerErrorCode GetBTSTListings(string stockCode, out List<EquityBTSTTradeBookRecord> btstTrades);

        BrokerErrorCode GetDematAllocation(string stockCode, out List<EquityDematHoldingRecord> holdings);

        BrokerErrorCode GetOpenPositionsPendingForDelivery(string stockCode, out List<EquityPendingPositionForDelivery> pendingPositions);

        BrokerErrorCode GetEquityTradeBookToday(bool newTradesOnly,
                string stockCode,
                out Dictionary<string, EquityTradeBookRecord> trades);

        BrokerErrorCode GetEquityOrderBookToday(bool newTradesOnly,
        bool bOnlyOutstandingOrders,
        string stockCode,
        out Dictionary<string, EquityOrderBookRecord> orders);

        BrokerErrorCode CancelEquityOrder(string orderRef, EquityOrderType productType);
        
        BrokerErrorCode CancelAllOutstandingEquityOrders(string stockCode,
            out Dictionary<string, BrokerErrorCode> cancelOrderErrCodes);

        BrokerErrorCode AllocateFunds(FundAllocationCategory category, double amountToAllocate);

        BrokerErrorCode GetFundsAvailable(out double fundAvailable);
    }
}
