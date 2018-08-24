using System;
using System.Collections.Generic;
using System.Diagnostics;
using StockTrader.Platform.Logging;
using StockTrader.Utilities;

namespace StockTrader.Core
{    
    public class EquityStockTradeStats //:
        //IComparable<EquityStockTradeStats>,
        //IComparer<EquityStockTradeStats>
    {

        // Constructors
        public EquityStockTradeStats(string stockCode) 
        {
            StockPrice = new Dictionary<Exchange,EquitySymbolQuote>();
            StockCode = stockCode;
            ResetBaseValues();
            ResetDynamicValues();
        }

        public EquityStockTradeStats(string stockCode,
            double fundLimitStarting, 
            int stockAvailableStarting,
            Exchange mainExchange,
            double lossLimit,
            double brokerageRate,
            double minBrokerage)
        {
            StockPrice = new Dictionary<Exchange,EquitySymbolQuote>();
            StockCode = stockCode;
            FundLimitAtStart = fundLimitStarting;
            StockAvailableAtStart = stockAvailableStarting;
            MainExchange = mainExchange;
            LossLimit = lossLimit;
            ResetDynamicValues();
        }

        public void SetEquityStockParams(string stockCode,
            double fundLimitStarting,
            int stockAvailableStarting,
            Exchange mainExchange,
            double lossLimit,
            double brokerageRate,
            double minBrokerage)
        {
            StockCode = stockCode;
            FundLimitAtStart = fundLimitStarting;
            StockAvailableAtStart = stockAvailableStarting;
            MainExchange = mainExchange;
            LossLimit = lossLimit;
            mBrokerageRate = brokerageRate;
            mMinBrokerage = minBrokerage;
            ResetDynamicValues();
        }


        protected void ResetBaseValues()
        {
            FundLimitAtStart = 0;
            StockAvailableAtStart = 0;
            LossLimit = 0;
            MainExchange = Exchange.NSE;
        }

        protected void ResetDynamicValues()
        {
            FundLimitCurrent = FundLimitAtStart;
            StockAvailableCurrent = StockAvailableAtStart;
            OrderFundLimit = FundLimitCurrent;
            OrderStockAvailable = StockAvailableCurrent;
            OrderBrokerage = 0;
            OutstandingBuyQty = 0;
            OutstandingSellQty = 0;
            TradeBrokerage = 0;
            TradeQtyBought = 0;
            TradeQtySold = 0;
            BuySellGain = 0;
            NetGain = 0;
        }

        // TODO: seems to be redundant
        protected void TriggerRefreshState()
        {

        }

        protected object lockObjectProperties = new object();
        
        private double mBrokerageRate = 0;
        private double mMinBrokerage = 0;

        protected object lockStateObject = new object();

        // Outstanding Orders and Trades list
        private Dictionary<string, EquityTradeBookRecord> mTrades = new Dictionary<string, EquityTradeBookRecord>();
        private Dictionary<string, EquityOrderBookRecord> mOutstandingOrders = new Dictionary<string, EquityOrderBookRecord>();
        private Dictionary<string, EquityOrderBookRecord> mOutstandingBuyOrders = new Dictionary<string, EquityOrderBookRecord>();
        private Dictionary<string, EquityOrderBookRecord> mOutstandingSellOrders = new Dictionary<string, EquityOrderBookRecord>();

        public Dictionary<string, EquityTradeBookRecord> Trades
        {
            get { lock (lockStateObject) { return mTrades; } }
            private set { lock (lockStateObject) { mTrades = value; } }
        }
        public Dictionary<string, EquityOrderBookRecord> OutstandingOrders
        {
            get { lock (lockStateObject) { return mOutstandingOrders; } }
            private set { lock (lockStateObject) { mOutstandingOrders = value; } }
        }
        public Dictionary<string, EquityOrderBookRecord> OutstandingBuyOrders
        {
            get { lock (lockStateObject) { return mOutstandingBuyOrders; } }
            private set { lock (lockStateObject) { mOutstandingBuyOrders = value; } }
        }
        public Dictionary<string, EquityOrderBookRecord> OutstandingSellOrders
        {
            get { lock (lockStateObject) { return mOutstandingSellOrders; } }
            private set { lock (lockStateObject) { mOutstandingSellOrders = value; } }
        }

        private int mNumTrades;
        public int NumTrades
        {
            get { lock (lockStateObject) { return mNumTrades; } }
            private set { lock (lockStateObject) { mNumTrades = value; } }
        }

        private int mNumOrders;
        public int NumOrders
        {
            get { lock (lockStateObject) { return mNumOrders; } }
            private set { lock (lockStateObject) { mNumOrders = value; } }
        }

        // Quantity bought as in current outstanding order book
        private int mOutstandingBuyQty;
        protected int OutstandingBuyQty
        {        

            get { lock(lockObjectProperties) {return mOutstandingBuyQty;} }
            set { lock (lockObjectProperties) { mOutstandingBuyQty = value;} }
        }

        // Quantity sold as in current outstanding order book
        private int mOutstandingSellQty;
        protected int OutstandingSellQty
        {
            get { lock(lockObjectProperties) {return mOutstandingSellQty;} }
            set { lock (lockObjectProperties) { mOutstandingSellQty = value;} }
        }

        // Quantity bought as in current trade book
        private int mTradeQtyBought;
        protected int TradeQtyBought
        {
            get { lock(lockObjectProperties) {return mTradeQtyBought;} }
            set { lock(lockObjectProperties) {mTradeQtyBought = value;} }
        }

        // Quantity sold as in current trade book
        private int mTradeQtySold;
        protected int TradeQtySold
        {
            get { lock(lockObjectProperties) {return mTradeQtySold;} }
            set { lock(lockObjectProperties) {mTradeQtySold = value;} }
        }

        // Stock specific current stock limit available for orders
        private double mOrderFundLimit;
        public double OrderFundLimit
        {
            get { lock(lockObjectProperties) {return mOrderFundLimit;} }
            protected set { lock(lockObjectProperties) {mOrderFundLimit = value;} }
        }

        // Stock specific current stock limit available for orders
        private int mOrderStockAvailable;
        public int OrderStockAvailable
        {
            get { lock(lockObjectProperties) {return mOrderStockAvailable;} }
            protected set { lock(lockObjectProperties) {mOrderStockAvailable = value;} }
        }

        // Estimated Brokerage fees in current outstanding orders
        private double mOrderBrokerage;
        public double OrderBrokerage
        {
            get { lock(lockObjectProperties) {return mOrderBrokerage;} }
            protected set { lock(lockObjectProperties) {mOrderBrokerage = value;} }
        }


        // Stock specific current funds limit remaining after executed trades
        private double mFundLimitCurrent;
        public double FundLimitCurrent
        {
            get { lock(lockObjectProperties) {return mFundLimitCurrent;} }
            protected set { lock(lockObjectProperties) {mFundLimitCurrent = value;} }
        }

        // Stock specific current stock limit exhausted int trades
        private int mStockAvailableCurrent;
        public int StockAvailableCurrent
        {
            get { lock(lockObjectProperties) {return mStockAvailableCurrent;} }
            protected set { lock(lockObjectProperties) {mStockAvailableCurrent = value;} }
        }

        // Net Profit/Loss in day's trade including brokerage
        private double mNetGain;
        public double NetGain
        {
            get { lock(lockObjectProperties) {return mNetGain;} }
            protected set { lock(lockObjectProperties) {mNetGain = value;} }
        }

        // Profit/Loss in day's trade without including brokerage
        private double mBuySellGain;
        public double BuySellGain
        {
            get { lock(lockObjectProperties) {return mBuySellGain;} }
            protected set { lock(lockObjectProperties) {mBuySellGain = value;} }
        }

        // Brokerage fees in day's trade
        private double mTradeBrokerage;
        public double TradeBrokerage
        {
            get { lock (lockObjectProperties) { return mTradeBrokerage; } }
            protected set { lock(lockObjectProperties) {mTradeBrokerage = value;} }
        }

        // Stock specific user set limit of loss
        private double mLossLimit;
        public double LossLimit
        {
            get { lock(lockObjectProperties) {return mLossLimit;} }
            set 
            {
                // New value cannot be less than what already is negative netgain
                if (NetGain >= 0 ? value >= 0: value >= Math.Abs(NetGain))
                {

                    lock(lockObjectProperties) {mLossLimit = value;}
                    TriggerRefreshState();
                }
            }
        }


        // Stock specific user set limit to use
        private double mFundLimitAtStart;
        public double FundLimitAtStart
        {
            get { lock(lockObjectProperties) {return mFundLimitAtStart;} }
            set 
            {
                // Cannot drastically reduce funds limit on the fly, since there may be some 
                // orders lined up or even the limit may already be used in trades
                // First cancel the orders to free up the limit then try to reduce

                // TODO: or automatically try to cancel some buy orders if possible
                // this much intelligence for later phase of project , not now :)
                if (value >= (FundLimitAtStart - FundLimitCurrent))
                {
                    FundLimitCurrent += (value - FundLimitAtStart);
                    lock(lockObjectProperties) {mFundLimitAtStart = value;}
                    TriggerRefreshState();
                }
            }
        }

        // Stock availability user set limit
        // these much must be available (to sell) in DP or in stock projection
        private int mStockAvailableAtStart;
        public int StockAvailableAtStart
        {
            get { lock(lockObjectProperties) {return mStockAvailableAtStart;} }
            set
            {
                // Cannot drastically reduce stocks limit on the fly, since there may be some 
                // orders lined up or even the limit may already be used in trades
                // First cancel the orders to free up the limit then try to reduce

                // TODO: or automatically try to cancel some sell orders if possible
                // this much intelligence for later phase of project , not now :)
                if (value >= (StockAvailableAtStart - StockAvailableCurrent))
                {
                    StockAvailableCurrent += (value - StockAvailableAtStart);
                    lock(lockObjectProperties) {mStockAvailableAtStart = value;}
                    TriggerRefreshState();
                }
            }
        }

        // Stock code
        public string StockCode { get; set; }

        // Main Exchange to deal with
        public Exchange MainExchange { get; protected set; }


        // Equity stock price info mapped to each exchange
        public Dictionary<Exchange, EquitySymbolQuote> StockPrice { get; private set; }

        public void AddOrUpdateStockPriceInExchange(EquitySymbolQuote stockPrice)
        {
            // do data consistency checks
            if (StockCode.CompareTo(stockPrice.StockCode) != 0)
            {
                throw new Exception("Equity StockPrice info's stock code doesnt match the holding StockInfo");
            }
            if (StockPrice.ContainsKey(stockPrice.Exchange))
            {
                StockPrice[stockPrice.Exchange] = stockPrice;
            }
            else
            {
                EquitySymbolQuote tmpStockPrice = stockPrice;
                StockPrice.Add(stockPrice.Exchange, stockPrice);
            }
        }

        // TODO: take into account same day buy sell brokerage
        public double EstimatedBrokerage(double transactionAmt)
        {
            return (Math.Max(transactionAmt * mBrokerageRate, mMinBrokerage));
        }


        // State values updated as non-incremental (i.e. each time whole of tradebook and orderbook are parsed)
        // Taking into account all trades, trade state variables are updated
        // then taking all outstanding orders, order state variables are updated 

        // NOTH: ordering of TRADE update before ORDER update is required because trades (profit :-) )
        // may have modified the FundLimitCurrent which is basically the limit available for orders
        public BrokerErrorCode RefreshState(Dictionary<string, EquityOrderBookRecord> orders,
            Dictionary<string, EquityTradeBookRecord> trades)
        {
            if (orders == null || trades == null)
            {
                return BrokerErrorCode.InValidArg;
            }

            BrokerErrorCode errorCode = BrokerErrorCode.Success;

            // lock for integrity of all STATE variable set [TODO: redundant lock, may remove]
            lock (lockStateObject)
            {
                // None of the properties should be accessed when they are getting updated 
                // as part of the state refresh, so hold the properties lock
                lock (lockObjectProperties)
                {
                    // reassign the new order and trade books
                    OutstandingOrders = orders;
                    Trades = trades;

                    // Reset the dynamic values
                    ResetDynamicValues();

                    NumTrades = Trades.Count;

                    // Update the Trade values
                    foreach (KeyValuePair<string, EquityTradeBookRecord> tradePair in Trades)
                    {
                        EquityTradeBookRecord tradeInfo = tradePair.Value;

                        if (tradeInfo.Direction == OrderDirection.BUY)
                        {
                            // Buy trades
                            // Quantity
                            TradeQtyBought += tradeInfo.Quantity;
                            StockAvailableCurrent += tradeInfo.Quantity;

                            // Trade or Estimated Brokerage
                            double tradeBrokerage = tradeInfo.Brokerage == 0 ? EstimatedBrokerage(tradeInfo.TradeValue) : tradeInfo.Brokerage;
                            TradeBrokerage += tradeBrokerage;

                            // Funds limit
                            FundLimitCurrent = FundLimitCurrent - tradeInfo.TradeValue - tradeBrokerage;
                            string traceString = "FundLimitCurrent should never go below ZERO";
                            Debug.Assert(FundLimitCurrent >= 0, traceString);
                            FileTracing.TraceOut(FundLimitCurrent < 0, traceString);
                        }
                        else
                        {
                            // Sell Trades
                            // Quantity
                            TradeQtySold += tradeInfo.Quantity;
                            StockAvailableCurrent -= tradeInfo.Quantity;
                            string traceString = "StockAvailableCurrent should never go below ZERO";
                            Debug.Assert(StockAvailableCurrent >= 0, traceString);
                            FileTracing.TraceOut(StockAvailableCurrent < 0, traceString);

                            // Trade or Estimated Brokerage
                            double tradeBrokerage = tradeInfo.Brokerage == 0 ? EstimatedBrokerage(tradeInfo.TradeValue) : tradeInfo.Brokerage;
                            TradeBrokerage += tradeBrokerage;

                            // Funds limit
                            FundLimitCurrent = FundLimitCurrent + tradeInfo.TradeValue - tradeBrokerage;
                            traceString = "FundLimitCurrent should never go below ZERO";
                            Debug.Assert(FundLimitCurrent >= 0, traceString);
                            FileTracing.TraceOut(FundLimitCurrent < 0, traceString);
                        }

                        // TODO: calculate brokerage properly for same day buy/sell trades
                    }

                    // HACKHACK: Dependency of Order limit values on Trade limit values being updated above 
                    OrderFundLimit = FundLimitCurrent;
                    OrderStockAvailable = StockAvailableCurrent;


                    // Also maintain the buy and sell outstanding orders separately
                    OutstandingBuyOrders.Clear();
                    OutstandingSellOrders.Clear();

                    NumOrders = OutstandingOrders.Count;

                    // Update the order values
                    foreach (KeyValuePair<string, EquityOrderBookRecord> orderPair in OutstandingOrders)
                    {
                        EquityOrderBookRecord orderInfo = orderPair.Value;

                        if (orderInfo.Direction == OrderDirection.BUY)
                        {
                            // Buy orders

                            OutstandingBuyOrders.Add(orderPair.Key, orderInfo);

                            // Quantity
                            OutstandingBuyQty += orderInfo.Quantity;
                            //because these order stocks are not really available until the buy order executes
                            //OrderStockAvailable += orderInfo.Quantity;

                            // Order value
                            double orderValue = orderInfo.Quantity * orderInfo.Price;

                            // Estimated Brokerage
                            double orderBrokerage = EstimatedBrokerage(orderValue);
                            OrderBrokerage += orderBrokerage;

                            // Funds limit
                            OrderFundLimit = OrderFundLimit - orderValue - orderBrokerage;
                            string traceString = "OrderFundLimit should never go below ZERO";
                            Debug.Assert(OrderFundLimit >= 0, traceString);
                            FileTracing.TraceOut(OrderFundLimit < 0, traceString);
                        }
                        else
                        {
                            // Sell orders

                            OutstandingSellOrders.Add(orderPair.Key, orderInfo);

                            // Quantity
                            OutstandingSellQty += orderInfo.Quantity;
                            OrderStockAvailable -= orderInfo.Quantity;
                            string traceString = "OrderStockAvailable should never go below ZERO";
                            Debug.Assert(OrderStockAvailable >= 0, traceString);
                            FileTracing.TraceOut(OrderStockAvailable < 0, traceString);

                            // Order value
                            double orderValue = orderInfo.Quantity * orderInfo.Price;

                            // Estimated Brokerage
                            double orderBrokerage = EstimatedBrokerage(orderValue);
                            OrderBrokerage += orderBrokerage;

                            // Funds limit will reduce because of to-be-incurred brokerage 
                            OrderFundLimit = OrderFundLimit - orderBrokerage;
                            traceString = "OrderFundLimit should never go below ZERO";
                            Debug.Assert(OrderFundLimit >= 0, traceString);
                            FileTracing.TraceOut(OrderFundLimit < 0, traceString);
                        }

                    }
                }
            }
            return errorCode;
        } // Refresh state ends

        public List<EquityOrder> ReturnFilteredValidOrders(List<EquityOrder> orders)
        {
            int numOrders = orders.Count;

            List<EquityOrder> feasibleOrders = new List<EquityOrder>();

            double orderFundLimit = OrderFundLimit;

            // TODO: BUGBUG make types coherent all across
            // some places we use uint for Quantity some places it is int
            int orderStockAvailable = OrderStockAvailable;

            // Need to weed out and return only the feasible orders as per stock's stock/fund limit
            for (int i = 0; i < numOrders; i++)
            {
                EquityOrder order = orders[i];
                if (order.OrderDirection == OrderDirection.BUY)
                {
                    // Buy trades

                    // Order value
                    double price = 0;
                    double orderValue = 0;

                    if (double.TryParse(order.LimitPrice, out price))
                    {
                        orderValue = order.Quantity * price;
                    }
                    else
                    {
                        FileTracing.TraceOut("ReturnFilteredValidOrders: Could not parse order.LimitPrice");
                        continue;
                    }

                    // Estimated Brokerage
                    double brokerage = EstimatedBrokerage(orderValue);

                    // Funds limit needed to fund this trade
                    double fundsNeededForTrade = orderValue + brokerage;

                    // If order limit is able to cover the order value (incl. brokerage) then it is a feasible order, add it
                    if (orderFundLimit >= fundsNeededForTrade)
                    {
                        orderFundLimit -= fundsNeededForTrade;
                        feasibleOrders.Add(order);
                    }
                }
                else
                {
                    // Sell Trades

                    // Order value
                    double price = 0;
                    double orderValue = 0;

                    if (double.TryParse(order.LimitPrice, out price))
                    {
                        orderValue = order.Quantity * price;
                    }
                    else
                    {
                        FileTracing.TraceOut("ReturnFilteredValidOrders: Could not parse order.LimitPrice");
                        continue;
                    }

                    // Estimated Brokerage
                    double orderBrokerage = EstimatedBrokerage(orderValue);

                    // If enough stocks are available to sell
                    // and order limit is able to cover the brokerage costs then it is a feasible order, add it
                    if (orderStockAvailable >= order.Quantity &&
                        orderFundLimit >= orderBrokerage)
                    {
                        orderFundLimit -= orderBrokerage;
                        orderStockAvailable -= order.Quantity;
                        feasibleOrders.Add(order);
                    }
                }
            }

            return feasibleOrders;
        } // ReturnFilteredValidOrders ends


        // Is any outstanding buy or sell orders
        public bool IsAnyOutstandingBuyOrder()
        {
            if (OutstandingBuyOrders.Count > 0)
            {
                return true;
            }
            return false;
        }
        public bool IsAnyOutstandingSellOrder()
        {
            if (OutstandingSellOrders.Count > 0)
            {
                return true;
            }
            return false;
        }

        public static BrokerErrorCode RefreshEquityStockStateToday(IBroker broker, EquityStockTradeStats stockInfo)
        {
            DateTime EarliestValidMarketOpenDate = MarketUtils.GetMarketCurrentDate();

            if (stockInfo == null)
            {
                return BrokerErrorCode.InValidArg;
            }

            return stockInfo.EquityRefreshStockState(broker, EarliestValidMarketOpenDate, EarliestValidMarketOpenDate);
        }

        public BrokerErrorCode EquityRefreshStockState(
            IBroker broker,
            DateTime fromDate,
            DateTime toDate)
        {
            return BrokerErrorCode.Success;

            //SIMPLETODO Remove above return and uncomment below code

            //BrokerErrorCode errorCode = BrokerErrorCode.Unknown;

            ////if (stockInfo == null)
            ////{
            ////    return BrokerErrorCode.InValidArg;
            ////}
            //Dictionary<string, EquityOrderBookRecord> orders = new Dictionary<string, EquityOrderBookRecord>();
            //Dictionary<string, EquityTradeBookRecord> trades = new Dictionary<string, EquityTradeBookRecord>();

            //// Get stock filtered Order book
            //errorCode = broker.GetEquityOrderBook(fromDate, toDate, false, true, StockCode, out orders);

            //if (errorCode == BrokerErrorCode.Success)
            //{
            //    // Get stock filtered Trade book
            //    errorCode = broker.GetEquityTradeBook(fromDate, toDate, false, StockCode, out trades);
            //}

            //if (errorCode == BrokerErrorCode.Success)
            //{
            //    // Call stock refresh method to update its state
            //    RefreshState(orders, trades);
            //}

            //return errorCode;
        }
        
        //public override string ToString()
        //{
        //    StringBuilder sb = new StringBuilder();
        //    sb.AppendLine("InstrumentType=" + mInstrumentType);
        //    sb.AppendLine("Underlying=" + mStockCode);
        //    sb.AppendLine("ExpiryDate=" + mExpiryDate);
        //    sb.AppendLine("LastTradePrice=" + mLastTradePrice);
        //    sb.AppendLine("LastTradedPrice=" + mLastTradedPrice);
        //    sb.AppendLine("OpenPrice=" + mOpenPrice);
        //    sb.AppendLine("HighPrice=" + mHighPrice);
        //    sb.AppendLine();
        //    return sb.ToString();
        //}

    }
}
