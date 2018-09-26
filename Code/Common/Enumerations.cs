
using System;

namespace StockTrader.Core
{
    
    public enum TradingSectionType
    {
        EQUITY,
        DERIVATIVES
    }
    public enum OrderPriceType
    {
        LIMIT,
        MARKET
    }

    public enum OrderGoodNessType
    {
        IOC,
        GTD
    }
    public enum Exchange
    {
        NSE,
        BSE
    }

    public enum OrderDirection
    {
        BUY,
        SELL
    }

    public enum EquityOrderType
    {
        DELIVERY,
        MARGIN
    }

    public enum InstrumentType
    {
        Share,
        OptionCallIndex,
        OptionCallStock,
        OptionPutIndex,
        OptionPutStock,
        FutureIndex,
        FutureStock
    }

    public enum DerivativePositionType
    {
        None = 0,
        Future = 1,
        Call = 2,
        Put = 4,
        All = 7
    }

    public enum Position
    {
        NONE,
        BUY,
        SELL
    }

    public enum PositionAllowAlgoCode
    {
        ALLOWNEWPOS,
        ALLOWSQUAREOFF,
        REJECT
    }

    public enum OrderPlaceAlgoCode
    {
        SUCCESS,
        RETRY,
        ALGORISKREJECT,
        ALGOLIMITREJECT,
        ALGOPAUSEREJECT
    }

    public enum EquityProductType
    {
        BTST,
        CASH,
        MARGIN,
        SPOT,
        MARGINPLUS
    }
    public enum OrderStatus
    {
        QUEUED,
        REQUESTED,
        ORDERED,
        PARTEXEC,
        COMPLETED,
        EXPIRED,
        REJECTED,
        CANCELLED,
        PARTEXECANDCANCELLED,
        NOTFOUND,
        UNKNOWN
    }

    public enum SolutionErrorCode
    {
        NotSupported,
        Unknown
    }

    public enum AlgoType
    {
        SimultaneousBuySell,
        AverageTheBuyThenSell
    }

    public enum OrderPositionTypeEnum
    {
        Demat,
        Btst,
        OpenPendingDelivery,
        Margin
    }

    public enum FundAllocationCategory
    {
        Equity,
        FnO,
        Currency,
        IpoMf
    }

    // This represents the Algo's Order placing state
    [Flags]
    public enum AlgoOrderPlaceState
    {
        NOTSTARTED,
        PAUSED,
        PAUSEDNEWPOS,
        RUNNING,
        FINISHED,
        STOPPED
    }

    [Flags]
    public enum BrokerErrorCode
    {
        Success = 0,
        ExchangeClosed,
        Http,
        ResourceNotAvailable,
        OrderNotAllowed,
        ContractNotEnabled,
        InValidContract,
        InValidStockCode,
        InsufficientLimit,
        InsufficientStock,
        InvalidLoginPassword,
        InvalidLotSize,
        Locked,
        NotLoggedIn,
        OutsidePriceRange,
        ServerError,
        TechnicalReason,
        NullResponse,
        OrderDoesNotExist,
        InValidOrderToCancel,
        OrderQueuedCannotCancel,
        OrderRejected,
        OrderExecutedCannotCancel,
        OrderAlreadyCancelled,
        OrderCancelFailed,
        InValidArg,
        NotSupported,
        ChangePassword,
        RemotePausedOrStopped,
        Unknown,
        FatalNoLoginTry
    }

    public enum TradeExecType
    {
        FreshLong,
        FreshShort,
        LongSquareOff,
        ShortSquareOff,
        None
    }

    public enum TickFormatType
    {
        IEOD,
        EOD,
        Custom,
        OnlyLTP
    }
}
