using System;
using System.Collections.Generic;
using StockTrader.Core;
using StockTrader.Platform.Logging;
using StockTrader.Utilities;
using StockTrader.Utilities.Broker;


// TODO: make the trading algo class generic to be able to plug-in with any broker
// BUGBUGDESIGN

// TODO: Implement mock
namespace StockTrader.API.TradingAlgos
{
    public class Algo1_AlgoParams
    {
        public Algo1_AlgoParams(int stockTradingLot,
                          bool bCancelOutstandingOrdersAtStartArg,
                          bool bStartAFreshArg,
                          bool bCancelOutstandingOrdersAtEndArg,
                          double initBidPriceArg,
                          double initOfferPriceArg,
                          bool bMock)
        {
            StockTradingLot = stockTradingLot;
            bCancelOutstandingOrdersAtStart = bCancelOutstandingOrdersAtStartArg;
            bCancelOutstandingOrdersAtStart = bCancelOutstandingOrdersAtStartArg;
            bStartAFresh = bStartAFreshArg;
            initBidPrice = initBidPriceArg;
            initOfferPrice = initOfferPriceArg;
            mMock = bMock;
        }

        // Stock trading lot(qty) for orders
        public int StockTradingLot { get; set; }

        public bool bCancelOutstandingOrdersAtStart;
        public bool bCancelOutstandingOrdersAtEnd;
        public bool bStartAFresh;
        public double initBidPrice;
        public double initOfferPrice;
        public bool mMock;
    }


    // Algo1
    public class Algo1_SimplePairedBuySellDelivery : ITradingAlgo
    {
        public BrokerErrorCode ErrorCode { get; set; }
        public bool DoStopAlgo { get; set; }
        public AlgoOrderPlaceState AlgoWorkingState { get; set; }
        public bool IsExternallySuspended { get; set; }
        public bool IsOrderExecutionPending { get; set; }

        // constructor
        public Algo1_SimplePairedBuySellDelivery(
             IBroker broker,
             EquityStockTradeStats stockInfoArg, 
             Algo1_AlgoParams algoParamsArg)
        {
            mBroker = broker;
            stockInfo = stockInfoArg;
            algoParams = algoParamsArg;

            // Derived properties
            stockCode = stockInfo.StockCode;
        }

        private EquityStockTradeStats stockInfo;
        private IBroker mBroker;
        private Algo1_AlgoParams algoParams;


        private string stockCode;
        private bool bGenerateASingleBuyOrderAtStart = false;
        private bool bGenerateASingleSellOrderAtStart = false;
        protected object lockObjectProperties = new object();

        // Stock trading lot(qty) for orders
        //private int mStockTradingLot;
        //protected int StockTradingLot
        //{

        //    get { lock (lockObjectProperties) { return mStockTradingLot; } }
        //    set { lock (lockObjectProperties) { mStockTradingLot = value; } }
        //}


        public BrokerErrorCode RunCoreAlgo()
        {
            #region SimpleBuySellPairSquareAlgo

            bool bCancelPairOrder = false;
            string stockCode = stockInfo.StockCode;
            int stockExchange = stockInfo.MainExchange == Exchange.NSE ? 0 : 1;

            BrokerErrorCode errorCode = BrokerErrorCode.Unknown;

            int tryCnt = 0;
            while (errorCode != BrokerErrorCode.Success && tryCnt <= 5)
            {
                // refresh stock state (doesnt have price info)
                errorCode = EquityStockState.EquityRefreshStockStateToday(mBroker, stockInfo);

                if (errorCode == BrokerErrorCode.NotLoggedIn)
                {
                    mBroker.LogOut();
                    errorCode = mBroker.CheckAndLogInIfNeeded(true);
                    tryCnt++;
                }
            }

            string traceString = "RefreshEquityStockStateToday: " + errorCode.ToString();
            FileTracing.TraceOut(traceString);

            if (errorCode.Equals(BrokerErrorCode.Success))
            {
                // If no buy or sell order pending, i.e. the square-off pair orders have executed
                // then place a pair of fresh buy-sell orders

                if (!(stockInfo.IsAnyOutstandingBuyOrder() ||
                    stockInfo.IsAnyOutstandingSellOrder()
                    ))
                {
                    // Place buy-sell order pair

                    // Get latest quote
                    EquitySymbolQuote[] stockPrice;

                    errorCode = mBroker.GetEquityQuote(stockCode, out stockPrice);

                    if (!errorCode.Equals(BrokerErrorCode.Success))
                    {
                        return errorCode;
                    }

                    // Generate orders
                    List<EquityOrderRecord> generatedOrders = new List<EquityOrderRecord>();

                    string BidPrice;
                    string OfferPrice;

                    // price decision

                    // First orders, only if market not open and it is first order pair
                    if ((stockInfo.NumTrades == 0) && !MarketUtils.IsMarketOpen())
                    {
                        double closePrice = stockPrice[stockExchange].PreviousClosePriceDouble;

                        if (algoParams.initBidPrice > 0)
                        {
                            BidPrice = algoParams.initBidPrice.ToString();
                        }
                        else
                        {
                            decimal buy = (decimal)(closePrice - (2.0 / 100 * closePrice));//prevclose - 3%
                            buy = decimal.Subtract(buy, 0.05M);
                            buy = decimal.Round(buy, 1);
                            BidPrice = buy.ToString();
                        }

                        if (algoParams.initOfferPrice > 0)
                        {
                            OfferPrice = algoParams.initOfferPrice.ToString();
                        }
                        else
                        {
                            decimal sell = (decimal)(closePrice + (2.0 / 100 * closePrice));//prevclose + 3%
                            sell = decimal.Add(sell, 0.05M);
                            sell = decimal.Round(sell, 1, MidpointRounding.AwayFromZero);
                            OfferPrice = sell.ToString();
                        }
                    }
                    // Subsequent orders
                    // TODO: May put time conditions or market volatility conditions here in else if
                    else
                    {
                        double ltp = stockPrice[stockExchange].LastTradePriceDouble;

                        decimal buy = (decimal)(ltp - ((double)(0.85 / 100) * ltp));//prevclose - 0.85%
                        buy = decimal.Subtract(buy, 0.05M);
                        buy = decimal.Round(buy, 1);
                        BidPrice = buy.ToString();

                        decimal sell = (decimal)(ltp + ((double)(0.85 / 100) * ltp));//prevclose + 0.85%
                        sell = decimal.Add(sell, 0.05M);
                        sell = decimal.Round(sell, 1, MidpointRounding.AwayFromZero);
                        OfferPrice = sell.ToString();
                    }

                    // Buy
                    generatedOrders.Add(new EquityOrderRecord(stockCode, algoParams.StockTradingLot, BidPrice, OrderPriceType.LIMIT, OrderDirection.BUY, stockInfo.MainExchange, EquityOrderType.DELIVERY, "0", "0"));
                    if (!bGenerateASingleBuyOrderAtStart)
                    {
                        // Sell
                        generatedOrders.Add(new EquityOrderRecord(stockCode, algoParams.StockTradingLot, OfferPrice, OrderPriceType.LIMIT, OrderDirection.SELL, stockInfo.MainExchange, EquityOrderType.DELIVERY, "0", "0"));
                    }
                    List<BrokerErrorCode> errorCodes = null;

                    // Validate orders
                    List<EquityOrderRecord> ordersToPlace = stockInfo.ReturnFilteredValidOrders(generatedOrders);


                    // Place validated orders

                    errorCodes = mBroker.PlaceMultipleEquityOrders(ordersToPlace, 5);

                    traceString = "Stock: " + stockInfo.StockCode + " :PlaceMultipleEquityOrders: 2\n";
                    int o = 0;
                    foreach (BrokerErrorCode errCodeEach in errorCodes)
                    {
                        EquityOrderRecord order = ordersToPlace[o++];
                        traceString = traceString + order.OrderDirection.ToString() + "-" + order.Quantity.ToString() +
                            " at " + order.Price + ": " + errCodeEach.ToString() + "\n";

                        // Place Order failed
                        if (!errCodeEach.Equals(BrokerErrorCode.Success))
                        {
                            // Cancel both the orders
                            bCancelPairOrder = true;
                        }
                    }
                    FileTracing.TraceOut(traceString);

                    // Even if any one of the orders failed to place, cancel the pair 
                    if (bCancelPairOrder)
                    {
                        errorCode = BrokerUtils.CancelStockOutstandingOrdersAndTrace(mBroker, stockCode);
                    }
                }
            }

            return errorCode;

            #endregion
        }

        public BrokerErrorCode RunCoreAlgoLive(SymbolTick si)
        {
            return RunCoreAlgo();
        }

        public BrokerErrorCode Prolog()
        {
            BrokerErrorCode errorCode = BrokerErrorCode.Success;
            string traceString = "Algo1_SimplePairedBuySellDelivery.Prolog :: ENTER";
            FileTracing.TraceOut(traceString);

            if (algoParams.bCancelOutstandingOrdersAtStart)
            {
                errorCode = BrokerUtils.CancelStockOutstandingOrdersAndTrace(mBroker, stockCode);
            }
            if (!algoParams.bStartAFresh)
            {
                // Start from last run state and try to be in pair-execute state
                // There may be less than or equal to 1 lot of stock as long or short
                // Pick up the last trade from xml
                // TODO:

            }
            // Get latest quote
            EquitySymbolQuote[] stockPrice;

            errorCode = mBroker.GetEquityQuote(stockCode, out stockPrice);

            if (!errorCode.Equals(BrokerErrorCode.Success))
            {
                return errorCode;
            }

            int stockExchange = stockInfo.MainExchange == Exchange.NSE ? 0 : 1;

            double buyOrderValue = (stockPrice[stockExchange].LastTradePriceDouble * algoParams.StockTradingLot);
            buyOrderValue += stockInfo.EstimatedBrokerage(buyOrderValue);


            // Erroneous conditions get out, do later
            // TODO: right now assume stock abvailable is at least = stock trading lot
            // and assume limit is available
            //if(stockInfo.StockAvailableAtStart < 


            // TODO: find better alternative to fit this variable
            if ((stockInfo.StockAvailableAtStart < algoParams.StockTradingLot) && (stockInfo.FundLimitAtStart >= 2 * buyOrderValue))
            {
                // This means first buy the stock, then start trading it
                bGenerateASingleBuyOrderAtStart = true;
            }
            else if (stockInfo.StockAvailableAtStart < algoParams.StockTradingLot)
            {
                errorCode = BrokerErrorCode.InValidArg;
            }

            if (!bGenerateASingleBuyOrderAtStart && errorCode.Equals(BrokerErrorCode.Success))
            {
                // There should be suficient stock to place a sell order to generate limit first
                // and then for the pair-orders
                // stockavailable should be at least twice the trading lot
                if ((stockInfo.FundLimitAtStart < buyOrderValue) && (stockInfo.StockAvailableAtStart >= 2 * algoParams.StockTradingLot))
                {
                    // Generate a sell order
                    bGenerateASingleSellOrderAtStart = true;
                }
                else if (stockInfo.FundLimitAtStart < buyOrderValue)
                {
                    errorCode = BrokerErrorCode.InValidArg;
                }
            }

            string price = "1";
            bool bGenerateOrder = false;
            OrderDirection orderDirection = OrderDirection.BUY;

            // BUY order
            if (bGenerateASingleBuyOrderAtStart)
            {
                // price decision
                if (algoParams.initBidPrice > 0)
                {
                    price = algoParams.initBidPrice.ToString();
                }
                // First orders, only if market not open and it is first order pair
                else if (!MarketUtils.IsMarketOpen())
                {
                    double closePrice = stockPrice[stockExchange].PreviousClosePriceDouble;

                    decimal buy = (decimal)(closePrice - ((double)(3.0 / 100) * closePrice));//prevclose - 3%
                    buy = decimal.Subtract(buy, 0.05M);
                    buy = decimal.Round(buy, 1);
                    price = buy.ToString();
                }
                // Subsequent orders
                // TODO: May put time conditions or market volatility conditions here in else if
                else
                {
                    double ltp = stockPrice[stockExchange].LastTradePriceDouble;

                    decimal buy = (decimal)(ltp - ((double)(0.65 / 100) * ltp));//prevclose - 0.65%
                    buy = decimal.Subtract(buy, 0.05M);
                    buy = decimal.Round(buy, 1);
                    price = buy.ToString();
                }
                orderDirection = OrderDirection.BUY;
                bGenerateASingleBuyOrderAtStart = false;
                bGenerateOrder = true;
            }


            // SELL order
            if (bGenerateASingleSellOrderAtStart)
            {
                // price decision
                if (algoParams.initOfferPrice > 0)
                {
                    price = algoParams.initOfferPrice.ToString();
                }
                // First orders, only if market not open and it is first order pair
                if (!MarketUtils.IsMarketOpen())
                {
                    double closePrice = stockPrice[stockExchange].PreviousClosePriceDouble;

                    decimal sell = (decimal)(closePrice + (3.0 / 100 * closePrice));//prevclose + 3%
                    sell = decimal.Add(sell, 0.05M);
                    sell = decimal.Round(sell, 1, MidpointRounding.AwayFromZero);
                    price = sell.ToString();
                }
                // Subsequent orders
                // TODO: May put time conditions or market volatility conditions here in else if
                else
                {
                    double ltp = stockPrice[stockExchange].LastTradePriceDouble;

                    decimal sell = (decimal)(ltp + (0.65 / 100 * ltp));//prevclose + 0.65%
                    sell = decimal.Add(sell, 0.05M);
                    sell = decimal.Round(sell, 1, MidpointRounding.AwayFromZero);
                    price = sell.ToString();
                }

                orderDirection = OrderDirection.SELL;
                bGenerateASingleSellOrderAtStart = false;
                bGenerateOrder = true;
            }

            if (bGenerateOrder)
            {
                // Generate orders
                List<EquityOrderRecord> generatedOrders = new List<EquityOrderRecord>
                                                              {
                                                                  new EquityOrderRecord(stockCode,
                                                                                        algoParams.StockTradingLot,
                                                                                        price, OrderPriceType.LIMIT,
                                                                                        orderDirection,
                                                                                        stockInfo.MainExchange,
                                                                                        EquityOrderType.DELIVERY, "0",
                                                                                        "0")
                                                              };

                List<BrokerErrorCode> errorCodes = null;

                // Validate orders
                List<EquityOrderRecord> ordersToPlace = stockInfo.ReturnFilteredValidOrders(generatedOrders);


                // Place validated orders

                errorCodes = mBroker.PlaceMultipleEquityOrders(ordersToPlace, 5);

                traceString = "Algo1_SimplePairedBuySellDelivery.Prolog\n" + "Stock: " + stockInfo.StockCode + " :PlaceMultipleEquityOrders: 1\n";
                int o = 0;
                foreach (BrokerErrorCode errCodeEach in errorCodes)
                {
                    EquityOrderRecord order = ordersToPlace[o++];
                    traceString = traceString + order.OrderDirection.ToString() + "-" + order.Quantity.ToString() +
                        " at " + order.Price + ": " + errCodeEach.ToString() + "\n";

                    errorCode = errCodeEach;
                }
                FileTracing.TraceOut(traceString);
            }
            traceString = "Algo1_SimplePairedBuySellDelivery.Prolog :: EXIT";
            FileTracing.TraceOut(traceString);

            return errorCode;
        }

        public BrokerErrorCode Epilog()
        {
            // While exiting the thread, cancel all the outstanding orders
            // Before cancel, store the state of any imbalanced outstanding order, i.e. 1-side executed(fully or partly) trade 
            BrokerErrorCode errorCode = BrokerErrorCode.Success;
            string traceString = "Algo1_SimplePairedBuySellDelivery.Epilog :: ENTER";
            FileTracing.TraceOut(traceString);
            if (algoParams.bCancelOutstandingOrdersAtEnd)
            {
                errorCode = BrokerUtils.CancelStockOutstandingOrdersAndTrace(mBroker, stockCode);
            }
            traceString = "Algo1_SimplePairedBuySellDelivery.Epilog :: EXIT";
            FileTracing.TraceOut(traceString);
            return errorCode;
        }

        public int GetSleepTimeInMilliSecs()
        {
            // 30 seconds sleep
            return 30 * 1000;
        }

        public string Description()
        {
            return "SimplePairedBuySell: " + stockCode;
        }
    }
}
