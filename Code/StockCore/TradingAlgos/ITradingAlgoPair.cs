using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using StockTrader.Common;
using StockTrader.Core;
using StockTrader.Platform.Logging;
using StockTrader.Utilities;
using StockTrader.Utilities.Broker;

// MD = MoreDebug
// DB = DataBase
// MC = MarketClosing
// SO = SquareOff
// EOD = End of Day
// EOP = End of Period

// SOMC-DB = SquareOff on MarketClosing, Runs on IntraDay Tick data and maintains EOD trading stats (profit/loss, num trades etc.) incl. EOP stats

namespace StockTrader.API.TradingAlgos
{
    public abstract partial class TradingAlgoPair : ITradingAlgo
    {
        public BrokerErrorCode ErrorCode { get; set; }
        protected bool bReplayDone = false;

        // Diagnostics
        public int TotalTimesLimitShortage = 0;
        public int TotalFailedOrders { get; protected set; }
        protected List<StockOrder> _allOrders = new List<StockOrder>();
        protected Dictionary<int, DTick> _allPeaks = new Dictionary<int, DTick>();
        protected List<double> _profitableOrders = new List<double>(1000);
        protected List<double> _lossOrders = new List<double>(1000);
        protected List<double> _nettProfitableOrders = new List<double>(1000);
        protected int _discontinuousMaxTickNum = 0;
        protected int _discontinuousMinTickNum = 0;
        protected DTick _lastPeak;
        protected double _currProfit = 0;
        protected DateTime _lastStateAlertTime = DateTime.MinValue;

        // Trade Summary
        public double LTP = 0;
        protected double _cummulativeProfitBooked = 0;
        protected double _nettFruitIfSquaredOffAtLTP = 0;
        protected double mAverageTradePrice = 0;
        public double TotalActualNettProfitAmt { get; protected set; }
        public double TotalExpectedNettProfitAmt { get; protected set; }
        public double TotalTurnover { get; protected set; }
        protected DateTime TimeOfFirstTickOfPeriod = DateTime.Now;
        protected DateTime TimeOfLastTickOfPeriod = DateTime.Now;
        public DateTime TimeOfFirstTickOfDay = DateTime.Now;
        public DateTime TimeOfLastTickOfDay = DateTime.Now;
        public int DayTotalTickConsideredCounts = 0;
        public EODTradeStats _eodTradeStats;
        public EOPTradeStats _eopTradeStats;
        protected List<DayNett> _dailyRunningNett = new List<DayNett>(1000);
        protected List<DayNett> _dailyRunningExpectedNett = new List<DayNett>(1000);

        // Algo Params
        public AlgoParams AlgoParams;

        // Algo run state (made properties due to part of Interface)
        private bool _doStopAlgo;
        public bool DoStopAlgo
        {
            get
            {
                return S != null ? S.DoStopAlgo : _doStopAlgo; //_doStopAlgo;
            }
            set
            {
                if (S != null)
                    S.DoStopAlgo = value;
                _doStopAlgo = value;
            }
        }
        private AlgoOrderPlaceState _algoWorkingState;
        public AlgoOrderPlaceState AlgoWorkingState
        {
            get
            {
                return S != null ? S.AlgoWorkingState : _algoWorkingState; //_algoWorkingState;
            }
            set
            {
                if (S != null)
                    S.AlgoWorkingState = value;
                _algoWorkingState = value;
            }
        }
        private bool _isExternallySuspended;
        public bool IsExternallySuspended
        {
            get
            {
                return S != null ? S.IsExternallySuspended : _isExternallySuspended;// _isExternallySuspended;
            }
            set
            {
                if (S != null)
                    S.IsExternallySuspended = value;
                _isExternallySuspended = value;
            }
        }

        // Algo calculations State
        public AlgoState S;

        // ------- Common auxillary methods -------- //
        protected double GetPriceForAnalysis(DerivativeSymbolQuote di)
        {
            //if (AlgoParams.UseProbableTradeValue)
            //{
            //    if (di.ProbableNextTradeValue != -1)
            //        return di.ProbableNextTradeValue;
            //}
            if (di.LastTradedPriceDouble > 0)
            {
                return di.LastTradedPriceDouble;
            }
            return di.LastTradedPriceDouble;
        }
        public double GetSquareOffProfitPerc()
        {
            bool isBuy = S.TotalBuyTrades < S.TotalSellTrades;
            double orderPrice = isBuy ? S.CurrTick.Di.BestOfferPriceDouble : S.CurrTick.Di.BestBidPriceDouble;
            double profitPoints = orderPrice - GetPairedSquareOffOrder(isBuy ? Position.BUY : Position.SELL, false).OrderPrice;
            profitPoints = isBuy ? -profitPoints : profitPoints;
            double brokerage = (AlgoParams.SquareOffBrokerageFactor * AlgoParams.PercBrokerage * orderPrice / 100);
            double nettProfit = profitPoints - brokerage;
            double profitPerc = (100 * nettProfit) / orderPrice;

            return profitPerc;
        }
        public double GetSquareOffProfitAmt()
        {
            double profitAmt = Math.Round((GetSquareOffProfitPerc() * AlgoParams.I.Qty * S.CurrTick.Di.LastTradedPriceDouble) / 100, 2);
            return profitAmt;
        }
        public StockOrder GetOpenPosition()
        {
            if (S.TotalBuyTrades != S.TotalSellTrades)
                return S.OpenPositions[0];

            return null;
        }
        protected StockOrder GetPairedSquareOffOrder(Position pos, bool isRemove)
        {
            StockOrder squaredOffOrder = null;
            for (int i = S.OpenPositions.Count - 1; i >= 0; i--)
            {
                if (S.OpenPositions[i].OrderPosition != pos)
                {
                    squaredOffOrder = S.OpenPositions[i];
                    if (isRemove)
                        S.OpenPositions.RemoveAt(i);
                    break;
                }
            }
            return squaredOffOrder;
        }
        protected void PostProcAddTick()
        {
            try
            {
                if (!AlgoParams.IsReplayMode)
                {
                    // Serialize the Algo State
                    using (Stream stream = File.Open(AlgoParams.StateFile, FileMode.Create))
                    {
                        var bformatter = new BinaryFormatter();
                        bformatter.Serialize(stream, S);
                    }

                    //// Alert on the current posiitons state
                    //if (S.TotalBuyTrades != S.TotalSellTrades)
                    //{
                    //    double profitPerc = Math.Round(GetSquareOffProfitPerc(), 2);
                    //    bool sendAlert = false;

                    //    //if ((profitPerc > 0.25 && ((profitPerc > 2 * _currProfit) || (profitPerc < 0.5 * _currProfit)))
                    //    //|| (profitPerc < 0.25 && ((profitPerc < 2 * _currProfit) || (profitPerc > 0.5 * _currProfit))))

                    //    if (DateTime.Now - _lastStateAlertTime > new TimeSpan(0, 15, 0))
                    //        sendAlert = true;

                    //    if (sendAlert)
                    //    {
                    //        double profitAmt = Math.Round((profitPerc * AlgoParams.I.Qty * S.CurrTick.Di.LastTradedPriceDouble) / 100, 2);
                    //        _currProfit = profitPerc;
                    //        _lastStateAlertTime = DateTime.Now;
                    //        var pnl = profitPerc > 0 ? "Profit" : "Loss";
                    //        var body = string.Format("Contract: {4}, {5}Amt: {0}, {5}Perc: {1}, CurrentPos: {2} at {3}. LTP: {6}",
                    //            profitAmt,
                    //            profitPerc,
                    //            S.OpenPositions[0].OrderPosition,
                    //            S.OpenPositions[0].OrderPrice,
                    //            AlgoParams.Description(),
                    //            pnl, S.CurrTick.Di.LastTradedPriceDouble);

                    //        MessagingUtils.SendAlertMessage(pnl + ":" + profitAmt, body);
                    //    }
                    //}
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }
        protected bool IsSquareOff(bool isBuy)
        {
            int numOpenPositions = Math.Abs(S.TotalBuyTrades - S.TotalSellTrades);
            bool isSquareOff = false;
            if (numOpenPositions != 0)
            {
                bool isMoreBuyOpenPositionsThanSell = S.TotalBuyTrades > S.TotalSellTrades;
                isSquareOff = !((isMoreBuyOpenPositionsThanSell && isBuy) || (!isMoreBuyOpenPositionsThanSell && !isBuy));
            }

            return isSquareOff;
        }
        protected PositionAllowAlgoCode GetPositionAllowCode(bool isBuy)
        {
            PositionAllowAlgoCode code = PositionAllowAlgoCode.REJECT;
            int numOpenPositions = Math.Abs(S.TotalBuyTrades - S.TotalSellTrades);
            int numNetBuyPos = S.TotalBuyTrades - S.TotalSellTrades;
            if (IsSquareOff(isBuy))
                code = PositionAllowAlgoCode.ALLOWSQUAREOFF;
            else
            {
                bool isLimitAvailable = IsSquareOff(isBuy) || (isBuy && (numNetBuyPos < AlgoParams.MaxLongPositions) && (numOpenPositions < AlgoParams.MaxTotalPositions))
                    || (!isBuy && (-numNetBuyPos < AlgoParams.MaxShortPositions) && (numOpenPositions < AlgoParams.MaxTotalPositions));

                if (isLimitAvailable) code = PositionAllowAlgoCode.ALLOWNEWPOS;
            }
            return code;
        }

        // ------- Core Algo methods start -------- //
        protected TradingAlgoPair(AlgoParams Params)
        {
            ErrorCode = BrokerErrorCode.Success;
            AlgoWorkingState = AlgoOrderPlaceState.NOTSTARTED;
            AlgoParams = Params;

            _eodTradeStats = new EODTradeStats(AlgoParams.R1, AlgoParams.R2);
            _eopTradeStats = new EOPTradeStats(AlgoParams.R1, AlgoParams.R2);

            S = new AlgoState();
            S.PercChangeThreshold = AlgoParams.PercMarketDirectionChange;

            FileTracing.TraceInformation(string.Format("Created Algo: {0} at {1}", AlgoParams.ToString(), DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString()));
        }
        public virtual void AddTick(SymbolTick si) { }

        protected virtual bool AlgoUpdateTick(SymbolTick si)
        {
            S.CurrTick = new DTick()
            {
                Di = si.D.Q,
                TickNumber = S.TotalTickCount
            };

            S.IsMarketClosing = MarketUtils.IsTimeAfter320(S.CurrTick.Di.UpdateTime);

            if (!AlgoParams.IsMock && !MarketUtils.IsTimeAfter915(S.CurrTick.Di.QuoteTime))  // start only when the day live quote appears
                return false;

            if (AlgoParams.AllowInitialTickStabilization && !MarketUtils.IsTimeAfter920(S.CurrTick.Di.UpdateTime))  // initial 5 minute no-trade buffer for the prices to settle down
                return false;

            // Stats
            double ltp = GetPriceForAnalysis(S.CurrTick.Di);
            mAverageTradePrice += ltp;
            TimeOfLastTickOfPeriod = S.CurrTick.Di.QuoteTime;

            // First tick of this algo, initialize daytoday and other variables
            if (!S.IsFirstAlgoTickSeen)
                DoFirstTickInititalizations();

            // Assuming that these algos are intraday while Live
            //if (AlgoParams.IsMock || AlgoParams.IsReplayMode)
            //    S.IsNextDay = (S.CurrTick.Di.QuoteTime.Date != S.DayToday.Date);

            // EOD Windup
            bool doesNeedEODWindup = AlgoParams.IsMarketClosingSquareOff && (S.IsMarketClosing /*|| S.IsNextDay*/) && !S.IsEODWindupDone;
            if (doesNeedEODWindup)
                DoEODWindup();

            //SetValuesForNextDay();

            S.PrevTick = S.CurrTick;

            //if (!(!S.IsNextDay && !S.IsEODWindupDone) && AlgoParams.IsMarketClosingSquareOff)
            //    return false;

            // *********** //
            if (S.IsEODWindupDone)
                return false;
            //*********** //

            // STATS
            // Analyzed ticks counting
            DayTotalTickConsideredCounts++;
            TimeOfLastTickOfDay = S.CurrTick.Di.QuoteTime;

            // Keeping track of Min and Max prices in the analyzed period
            if (ltp < _eopTradeStats.min_price)
                _eopTradeStats.min_price = ltp;
            else if (ltp > _eopTradeStats.max_price)
                _eopTradeStats.max_price = ltp;

            if (ltp < _eodTradeStats.min_price)
                _eodTradeStats.min_price = ltp;
            else if (ltp > _eodTradeStats.max_price)
                _eodTradeStats.max_price = ltp;

            return true;
        }
        protected void UpdateMinMax()
        {
            // Update Min Max
            DerivativeSymbolQuote maxDi = S.MaxTick.Di;
            DerivativeSymbolQuote minDi = S.MinTick.Di;
            S.PercChangeFromMax = 100 * ((GetPriceForAnalysis(S.CurrTick.Di) - GetPriceForAnalysis(maxDi)) / GetPriceForAnalysis(maxDi));
            S.PercChangeFromMin = 100 * ((GetPriceForAnalysis(S.CurrTick.Di) - GetPriceForAnalysis(minDi)) / GetPriceForAnalysis(minDi));

            if (S.PercChangeFromMax >= 0)
            {
                // New Max
                S.MaxTick = S.CurrTick;
            }
            else if (S.PercChangeFromMin <= 0)
            {
                // New Min
                S.MinTick = S.CurrTick;
            }

            // Get trend
            if (Math.Abs(S.CurrTick.Di.PercChangeFromPrevious) > 1.5)
            {
                S.DominantDirection = S.CurrTick.Di.PercChangeFromPrevious > 0 ? MarketDirection.UP : MarketDirection.DOWN;
            }
        }
        protected virtual bool FindPeaksAndOrder()
        {
            OrderPlaceAlgoCode orderPlaceCode = OrderPlaceAlgoCode.ALGOLIMITREJECT;
            // 1. CHECK NEW TOP (Always occur for square off trade only)
            if (S.LastPeakType == PeakType.BOTTOM)
            {
                if (S.PercChangeFromMax < 0 && Math.Abs(S.PercChangeFromMax) >= S.PercChangeThreshold)
                {
                    // Record tick abberations
                    if (Math.Abs(S.PercChangeFromMax) - S.PercChangeThreshold > 1)
                    {
                        Logger.LogWarningMessage(
                             string.Format("{3}: {0} : PriceTick non-continuous {1}: PercThreshold = {4} S.PercChangeFromMax = {2} times.",
                                           S.CurrTick.Di.QuoteTime, _discontinuousMaxTickNum++,
                                           S.PercChangeFromMax / S.PercChangeThreshold, AlgoParams.I.Symbol, S.PercChangeThreshold));
                    }

                    orderPlaceCode = AlgoTryPlaceOrder(false, S.CurrTick); // Sell

                    if (orderPlaceCode == OrderPlaceAlgoCode.SUCCESS)
                    {
                        // New TOP found
                        S.LastPeakType = PeakType.TOP;
                        _lastPeak = S.MaxTick;
                        S.MinTick = S.CurrTick;
                    }
                }
            }
            // 2. CHECK NEW BOTTOM (Always occur for square off trade only)
            else if (S.LastPeakType == PeakType.TOP)
            {
                // Peak - New trades or pending squareoffs
                if (S.PercChangeFromMin > 0 && Math.Abs(S.PercChangeFromMin) >= S.PercChangeThreshold)
                {
                    // Record tick abberations
                    if (Math.Abs(S.PercChangeFromMin) - S.PercChangeThreshold > 1)
                    {
                        Logger.LogWarningMessage(
                             string.Format("{3}: {0} : PriceTick non-continuous {1}: PercThreshold = {4} S.PercChangeFromMin = {2} times.",
                                           S.CurrTick.Di.QuoteTime, _discontinuousMaxTickNum++,
                                           S.PercChangeFromMax / S.PercChangeThreshold, AlgoParams.I.Symbol, S.PercChangeThreshold));
                    }

                    orderPlaceCode = AlgoTryPlaceOrder(true, S.CurrTick); // Buy

                    if (orderPlaceCode == OrderPlaceAlgoCode.SUCCESS)
                    {
                        // New BOTTTOM found
                        S.LastPeakType = PeakType.BOTTOM;
                        _lastPeak = S.MinTick;
                        S.MaxTick = S.CurrTick;
                    }

                }
            }
            // 3. 1st or FRESH position PEAK (TOP/BOTTOM)  (Never occurs for square off trade and always occur for fresh trade)
            else if (S.LastPeakType == PeakType.NONE)
            {
                bool isBuy = S.PercChangeFromMin > 0 && Math.Abs(S.PercChangeFromMin) >= S.PercChangeThreshold;
                bool isSell = S.PercChangeFromMax < 0 && Math.Abs(S.PercChangeFromMax) >= S.PercChangeThreshold;

                if (isBuy && isSell)
                {
                    // May occur due to order not getting executed. But in offline analysis should not occur ideally 
                    var msg = string.Format("Error: NONE peak says both buy and sell." +
                        "Some bug. Overcome is assume buy. max={0}, min={1}, th={2}", S.PercChangeFromMax, S.PercChangeFromMin, S.PercChangeThreshold);
                    FileTracing.TraceOut(msg);
                    isBuy = Math.Abs(S.PercChangeFromMin) > Math.Abs(S.PercChangeFromMax);
                    isSell = !isBuy;
                }
                if (isBuy || isSell)
                {
                    orderPlaceCode = AlgoTryPlaceOrder(isBuy, S.CurrTick); // buy or sell

                    if (orderPlaceCode == OrderPlaceAlgoCode.SUCCESS)
                    {
                        // Set last peak as reverse of current tick direction
                        S.LastPeakType = isBuy ? PeakType.BOTTOM : PeakType.TOP;

                        if (S.LastPeakType == PeakType.BOTTOM)
                        {
                            // 1st peak is BOTTOM
                            _lastPeak = S.MinTick;
                        }
                        else
                        {
                            // 1st peak is TOP
                            _lastPeak = S.MaxTick;
                        }
                    }
                }
            }

            if (orderPlaceCode == OrderPlaceAlgoCode.SUCCESS)
            {
                var tickNum = S.TotalTickCount;
                while (_allPeaks.ContainsKey(tickNum))
                    tickNum += 1;

                _allPeaks.Add(tickNum, _lastPeak);
            }

            return orderPlaceCode == OrderPlaceAlgoCode.SUCCESS;
        }
        protected virtual OrderPlaceAlgoCode AlgoTryPlaceOrder(bool isBuy, DTick tick, bool doNoPriceCheck = false
            , bool isMarketOrder = false, bool passThroughOrder = false)
        {
            // Check on Algo order placement state 
            if (!isMarketOrder || !passThroughOrder)
            {
                if (AlgoWorkingState != AlgoOrderPlaceState.RUNNING)
                {
                    // New positions are paused
                    if (AlgoWorkingState == AlgoOrderPlaceState.PAUSEDNEWPOS && !IsSquareOff(isBuy))
                        return OrderPlaceAlgoCode.ALGOPAUSEREJECT;

                    else if (AlgoWorkingState != AlgoOrderPlaceState.PAUSEDNEWPOS)
                        return OrderPlaceAlgoCode.ALGOPAUSEREJECT;
                }
            }

            // Do not take new positions after 3.15
            if (MarketUtils.IsTimeAfter315(S.CurrTick.Di.UpdateTime) && !IsSquareOff(isBuy))
                return OrderPlaceAlgoCode.ALGORISKREJECT;

            DerivativeSymbolQuote di = tick.Di;
            //OrderPlaceAlgoCode orderPlaceAlgoCode = OrderPlaceAlgoCode.ALGOLIMITREJECT;

            PositionAllowAlgoCode posCode = GetPositionAllowCode(isBuy);

            // Order not allowed due to limit not available
            if (posCode == PositionAllowAlgoCode.REJECT)
            {
                TotalTimesLimitShortage++;
                FileTracing.TraceInformation("Not sufficient limit to place order: Number " + TotalTimesLimitShortage);
                return OrderPlaceAlgoCode.ALGOLIMITREJECT;
            }

            // Limit 1 trade per day
            if (AlgoParams.IsFixedTradesPerDay)
            {
                if (_eodTradeStats.num_trades == AlgoParams.NumTradesStopForDay && (S.TotalBuyTrades == S.TotalSellTrades))
                {
                    //AlgoWorkingState = AlgoRunState.TRADESPAUSED;
                    return OrderPlaceAlgoCode.ALGORISKREJECT;
                }
            }

            // Limit Risk
            // Risk management- no new trades if loss limits are crossed (stop loss after N trades if in loss or certain daily loss limit)
            if (AlgoParams.IsLimitLossPerDay)
            {
                if (posCode != PositionAllowAlgoCode.ALLOWSQUAREOFF)
                {
                    if (_eodTradeStats.actual_profit < 0)
                    {
                        var percLoss = Math.Abs((_eodTradeStats.actual_profit * 100) / (_eodTradeStats.min_price * AlgoParams.I.Qty));

                        // 1 & 2 are new conditiosn when running 46, 47 and 48
                        bool isStopLossHit1 = (percLoss >= AlgoParams.PercPnLStopForDay * 0.75) && _eodTradeStats.num_loss_trades >= AlgoParams.NumTradesStopForDay;
                        bool isStopLossHit2 = (percLoss >= AlgoParams.PercPnLStopForDay * 0.75) && _eodTradeStats.num_loss_trades >= 2 && _eodTradeStats.num_profit_trades == 0;

                        bool isStopLossHit3 = percLoss >= AlgoParams.PercPnLStopForDay ||
                            ((_eodTradeStats.num_loss_trades - _eodTradeStats.num_profit_trades) >= AlgoParams.NumTradesStopForDay);

                        if (isStopLossHit3)//isStopLossHit1 || isStopLossHit2)
                        {
                            //AlgoWorkingState = AlgoRunState.TRADESPAUSED;
                            string info = string.Format("STOPLOSS: Not taking new trade of {0}." +
                                "Day L = {1}, PercL = {2}, Trades = {3}," +
                                "NumP/AvgP = {4} # {5}, NumL/AvgL = {6} # {7}",
                                AlgoParams.I.Description(), _eodTradeStats.actual_profit,
                                (_eodTradeStats.actual_profit / (tick.Di.LastTradedPriceDouble * AlgoParams.I.Qty)) * 100,
                                _eodTradeStats.num_trades,
                                _eodTradeStats.num_profit_trades,
                                _eodTradeStats.num_profit_trades == 0 ? 0 : _eodTradeStats.average_profit_pertrade / _eodTradeStats.num_profit_trades,
                                _eodTradeStats.num_loss_trades,
                                _eodTradeStats.num_loss_trades == 0 ? 0 : _eodTradeStats.average_loss_pertrade / _eodTradeStats.num_loss_trades);

                            // Just to optimize offline analysis
                            if (AlgoParams.IsMock || AlgoParams.IsReplayMode)
                                DoStopAlgo = true;

                            FileTracing.TraceOut(info);
                            return OrderPlaceAlgoCode.ALGORISKREJECT;
                        }
                    }
                }
            }

            bool isOrderExecuted = false;
            string tradeConfirmation = "";
            var orderDirection = isBuy ? Position.BUY : Position.SELL;
            var newOrder = new StockOrder(orderDirection, 0);
            isOrderExecuted = PlaceOrder(newOrder, di, doNoPriceCheck, isMarketOrder);

            if (!isOrderExecuted)
                return OrderPlaceAlgoCode.RETRY;

            if (isOrderExecuted)
            {
                if (newOrder.OrderPosition == Position.BUY)
                {
                    S.TotalBuyTrades++;
                    _eodTradeStats.num_trades++;
                }
                else if (newOrder.OrderPosition == Position.SELL)
                {
                    S.TotalSellTrades++;
                }
                TotalTurnover += newOrder.OrderPrice * AlgoParams.I.Qty;

                tradeConfirmation = string.Format("Trade Confirmed: {0} {1} {4} qty at {2}. OrderRef= {3}", AlgoParams.I.ToString(), newOrder.OrderPosition, newOrder.OrderPrice, newOrder.OrderRef,
                    AlgoParams.I.Qty);
                FileTracing.TraceImpAlgoInformation(tradeConfirmation);

                // Expected price calculations
                try
                {
                    newOrder.ExpectedOrderPrice = newOrder.OrderPrice;

                    if (!S.IsMarketClosing && !(S.IsNextDay && !AlgoParams.IsMarketClosingSquareOff))
                    {
                        double expectedPriceDiffPerc = 0;
                        if (newOrder.OrderPosition == Position.BUY)
                            expectedPriceDiffPerc = Math.Abs(S.PercChangeFromMin) - S.PercChangeThreshold;
                        else
                            expectedPriceDiffPerc = Math.Abs(S.PercChangeFromMax) - S.PercChangeThreshold;
                        // Ideal conditions Order price setting. Not taking Bid/Ask price exactly (in data with us they are unavailable)
                        // just using LTP
                        // On next day, market opens afresh (may be highly different from previous  close (previous tick)) 
                        // so order price will be same as LTP
                        // but only when continuous algo (algo id 11) functions, because for SquareOff on marketClosing algo,
                        // anyway the squareoff was done and 1st trade will neevr be on 1st tick of nextDay
                        // in algo9 SO on 1st tick of next day is done only if previous day data was incomplete or something

                        newOrder.ExpectedOrderPrice += (newOrder.ExpectedOrderPrice * (expectedPriceDiffPerc / 100));
                    }
                }
                catch (Exception ex)
                {
                    string msg = string.Format("Exception in AlgoTryPlaceOrder expectedPrice calculations. \nError: {0}\n Stack:{1}", ex.Message, ex.StackTrace);
                    FileTracing.TraceOut(msg);
                }

                // it is a Squareoff order
                if (posCode == PositionAllowAlgoCode.ALLOWSQUAREOFF)
                {
                    StockOrder squaredOffOrder = GetPairedSquareOffOrder(newOrder.OrderPosition, true);

                    double nettProfitAmt = AfterSquareOff(newOrder, squaredOffOrder);

                    string squareOffTradeDesc = string.Format("SquareOff: NettProfitAmt= " + (int)nettProfitAmt);
                    FileTracing.TraceImpAlgoInformation(squareOffTradeDesc);
                    tradeConfirmation += "\n" + squareOffTradeDesc;
                }
                // It is new order
                else
                {
                    S.OpenPositions.Add(newOrder);
                }

                // Add to Global orders list
                _allOrders.Add(newOrder);
            }

            if (!AlgoParams.IsMock && isOrderExecuted)
                MessagingUtils.SendAlertMessage("Trade Confirmed", tradeConfirmation);

            return OrderPlaceAlgoCode.SUCCESS;
        }
        protected double AfterSquareOff(StockOrder newOrder, StockOrder squaredOffOrder)
        {
            if (newOrder.OrderPosition == Position.NONE)
                throw new Exception("Unexpected Invalid Position NONE for new order");

            double profitAmount = (newOrder.OrderPrice - squaredOffOrder.OrderPrice) * AlgoParams.I.Qty;
            if (newOrder.OrderPosition == Position.BUY)
                profitAmount = -profitAmount;

            if (profitAmount >= 0)
                _profitableOrders.Add(profitAmount);


            double brokerageAmt = (AlgoParams.SquareOffBrokerageFactor * AlgoParams.PercBrokerage * AlgoParams.I.Qty * newOrder.OrderPrice / 100);
            double nettProfitAmt = profitAmount - brokerageAmt;

            // Set Last trade profit perc
            S.LastSquareOffPercProfit = (nettProfitAmt * 100) / (squaredOffOrder.OrderPrice * AlgoParams.I.Qty);

            if (nettProfitAmt >= 0)
                _nettProfitableOrders.Add(nettProfitAmt);
            else
                _lossOrders.Add(nettProfitAmt);

            TotalActualNettProfitAmt += nettProfitAmt;
            S.TotalBrokerageAmt += brokerageAmt;

            _eodTradeStats.actual_profit += nettProfitAmt;
            _eodTradeStats.brokerage += brokerageAmt;

            // Ideal conditions expected nettProfit
            // Expected loss should be threshold percent at max in ideal conditions.
            double expectedProfitAmount = (newOrder.ExpectedOrderPrice - squaredOffOrder.ExpectedOrderPrice) * AlgoParams.I.Qty;
            if (newOrder.OrderPosition == Position.BUY)
                expectedProfitAmount = -expectedProfitAmount;

            double expectedNettProfit = expectedProfitAmount - brokerageAmt;

            TotalExpectedNettProfitAmt += expectedNettProfit;
            _eodTradeStats.expected_profit += expectedNettProfit;

            // Expected profit should always be greater. If not, then either there is a bug in code or tick data is incorrect.
            //Debug.Assert(nettProfitAmt - expectedNettProfit <= 0);

            if (nettProfitAmt < 0)
            {
                _eodTradeStats.average_loss_pertrade += nettProfitAmt;
                _eodTradeStats.num_loss_trades++;

                _eopTradeStats.average_loss_pertrade += nettProfitAmt;
                _eopTradeStats.num_loss_trades++;
            }
            else if (nettProfitAmt > 0)
            {
                _eodTradeStats.average_profit_pertrade += nettProfitAmt;
                _eodTradeStats.num_profit_trades++;

                _eopTradeStats.average_profit_pertrade += nettProfitAmt;
                _eopTradeStats.num_profit_trades++;
            }

            return nettProfitAmt;
        }

        // --------------------------------------------//
        protected void DoFirstTickInititalizations()
        {
            // First tick of this algo, initialize daytoday and other variables
            if (!S.IsFirstAlgoTickSeen)
            {
                double ltp = GetPriceForAnalysis(S.CurrTick.Di);
                S.IsFirstAlgoTickSeen = true;
                TimeOfFirstTickOfPeriod = S.CurrTick.Di.QuoteTime;
                TimeOfFirstTickOfDay = S.CurrTick.Di.QuoteTime;
                S.DayToday = S.CurrTick.Di.QuoteTime;
                S.IsEODWindupDone = false;
                S.MinTick = S.MaxTick = S.PrevTick = S.CurrTick;

                _eopTradeStats.max_price = _eopTradeStats.min_price = ltp;
                _eodTradeStats.max_price = _eodTradeStats.min_price = ltp;
            }
        }
        protected bool DoEODWindup()
        {
            bool doesNeedEODWindup = AlgoParams.IsMarketClosingSquareOff && (S.IsMarketClosing || S.IsNextDay) && !S.IsEODWindupDone;
            bool doStatsEODWindup = false;

            // Positions windup (squareoff)
            if (doesNeedEODWindup)
            {
                int numOpenPositions = S.TotalBuyTrades - S.TotalSellTrades;

                if (numOpenPositions != 0)
                {
                    bool isBuy = numOpenPositions < 0 ? true : false;
                    bool placeMarketOrder = MarketUtils.IsTimeAfter325(DateTime.Now); // place market order if not success to squareoff by 3.25 pm

                    OrderPlaceAlgoCode closingOrderCode = AlgoTryPlaceOrder(isBuy, S.CurrTick, false, placeMarketOrder, true);

                    //if (closingOrderCode != OrderPlaceAlgoCode.SUCCESS)
                    //    return false;  // Come again with another tick and then try

                    ////if (closingOrderCode == OrderPlaceAlgoCode.SUCCESS)
                    //isEODPositionSquareOffDone = S.TotalBuyTrades == S.TotalSellTrades;

                    if (S.TotalBuyTrades != S.TotalSellTrades)
                        return false;
                }
                doStatsEODWindup = true;
            }

            // STATS Windup
            if (doStatsEODWindup)
            {
                _eodTradeStats.number_of_ticks = DayTotalTickConsideredCounts;
                _eopTradeStats.number_of_ticks += _eodTradeStats.number_of_ticks;
                _eodTradeStats.inmarket_time_in_minutes = (int)(TimeOfLastTickOfDay - TimeOfFirstTickOfDay).TotalMinutes;
                _eopTradeStats.inmarket_time_in_minutes += _eodTradeStats.inmarket_time_in_minutes;
                _eopTradeStats.num_days++;

                _eodTradeStats.status_update_time = DateTime.Now;
                _eodTradeStats.trade_date = S.DayToday.Date;
                _eodTradeStats.algo_id = AlgoParams.AlgoId;
                _eodTradeStats.contract_name = AlgoParams.I.Symbol;

                _eodTradeStats.average_profit_pertrade = _eodTradeStats.num_profit_trades == 0
                                                            ? 0
                                                            : _eodTradeStats.average_profit_pertrade /
                                                              _eodTradeStats.num_profit_trades;
                _eodTradeStats.average_loss_pertrade = _eodTradeStats.num_loss_trades == 0
                                                          ? 0
                                                          : _eodTradeStats.average_loss_pertrade /
                                                            _eodTradeStats.num_loss_trades;

                // Because money invested is only 25% (margin) for derivatives, thus multiplication factor of 4 by default
                _eodTradeStats.roi_percentage = (_eodTradeStats.expected_profit / (S.PrevTick.Di.LastTradedPriceDouble * AlgoParams.I.Qty)) *
                                                100 * (1 / AlgoParams.MarginFraction);
                _eodTradeStats.actual_roi_percentage = (_eodTradeStats.actual_profit / (S.PrevTick.Di.LastTradedPriceDouble * AlgoParams.I.Qty)) *
                                                      100 * (1 / AlgoParams.MarginFraction);
                _eodTradeStats.market_direction_percentage = AlgoParams.PercMarketDirectionChange;
                _eodTradeStats.quantity = AlgoParams.I.Qty;

                // Insert EodSummary into DB
                _eodTradeStats.Persist();

                // Reset the variables for next day
                DayTotalTickConsideredCounts = 0;
                S.MinTick = S.MaxTick = _lastPeak = null;
                S.LastPeakType = PeakType.NONE;

                S.IsEODWindupDone = true;
            }

            return true;
        }
        protected void SetValuesForNextDay()
        {
            // Will never hit in EOD data and intraday live (because it is same day), because mIsMarketClosingSquareOff is false and hence isEODWindupDone is false always
            if (S.IsNextDay && S.IsEODWindupDone)
            {
                // Start of next day
                S.IsEODWindupDone = false;
                S.DayToday = S.CurrTick.Di.QuoteTime;

                _eodTradeStats = new EODTradeStats(AlgoParams.R1, AlgoParams.R2);
                _eodTradeStats.max_price = _eodTradeStats.min_price = GetPriceForAnalysis(S.CurrTick.Di);

                if (AlgoParams.IsConsiderPrevClosing)
                {
                    S.MinTick = S.MaxTick = S.PrevTick;
                }
                else
                {
                    if (S.MinTick == null || S.MaxTick == null)
                        S.MinTick = S.MaxTick = S.CurrTick;
                }

                TimeOfFirstTickOfDay = S.CurrTick.Di.QuoteTime;
                TimeOfLastTickOfDay = S.CurrTick.Di.QuoteTime;
                DayTotalTickConsideredCounts++;
            }

            if (S.IsNextDay)
                S.DayToday = S.CurrTick.Di.QuoteTime;
        }

        // ------- Order related methods -------- //
        protected bool PlaceOrder(StockOrder orderToPlace, DerivativeSymbolQuote di, bool doNoPriceCheck = true, bool isMarketOrder = false)
        {
            Position position = orderToPlace.OrderPosition;
            double executionPrice = -1;

            OrderDirection orderDirection;
            double orderPrice;
            bool hasOrderExecuted;
            double diff = 0;

            if (position == Position.BUY)
            {
                orderPrice = di.BestOfferPriceDouble;
                diff = di.LastTradedPriceDouble - orderPrice;
                orderDirection = OrderDirection.BUY;
            }
            else if (position == Position.SELL)
            {
                orderPrice = di.BestBidPriceDouble;
                diff = orderPrice - di.LastTradedPriceDouble;
                orderDirection = OrderDirection.SELL;
            }
            else
            {
                FileTracing.TraceOut(string.Format("{0} Invalid Position = {1}", AlgoParams.I.Description(), position));
                return false;
            }

            string str = di.UpdateTime.ToLongDateString() + " " + di.UpdateTime.ToLongTimeString() + ". Placing order \"" + AlgoParams.I.Description() + "\". Position=" + position.ToString() + ". OrderPrice=" + orderPrice + ". Quantity=" + AlgoParams.I.Qty;
            FileTracing.TraceTradeInformation(str);
            //Beep(100, 50);

            if (!isMarketOrder && !doNoPriceCheck)
            {
                // Check orderprice and ltp difference
                double diffPerc = (diff * 100) / di.LastTradedPriceDouble;
                if (diff < 0 && Math.Abs(diffPerc) > 0.1)
                {
                    FileTracing.TraceOut(string.Format("{0} . Not ordering at {4}. LTP = {3}. Spread diff = {1}, diff % = {2}",
                        AlgoParams.I.Description(), diff, diffPerc, di.LastTradedPriceDouble, orderPrice));
                    return false;
                }
            }

            // ---- Place order
            while (true)
            {
                BrokerErrorCode errorCode = BrokerErrorCode.Success;
                double orderPriceAdjusted = 0;
                if (AlgoParams.IsMock || AlgoParams.IsReplayMode)
                {
                    errorCode = BrokerErrorCode.Success;
                    orderToPlace.OrderRef = "IsMock Order";
                }
                else
                {
                    // Add a price margin to definitly get the trade
                    double priceMargin = 0;
                    try
                    {
                        priceMargin = Math.Round(orderPrice * 0.0001, 2);
                        double a = Math.Floor(priceMargin);
                        double a2 = Math.Round((priceMargin - a) * 100);
                        double a3 = a2 % 5;
                        a2 = a + (a2 - a3) / 100;
                        priceMargin = Math.Max(a2, 0.05);

                        if (Math.Abs(priceMargin) > 0.05 && ((Math.Abs(priceMargin) * 100) / orderPrice) > 0.02)
                            priceMargin = 0.05;


                        if (position == Position.SELL)
                            priceMargin = -priceMargin;
                    }
                    catch (Exception e)
                    {
                        priceMargin = 0;
                        Logger.LogException(e);
                    }

                    //hack
                    //if (!isMarketOrder)
                    //{
                    //    isMarketOrder = AlgoParams.I.Symbol == "DLFLIM";
                    //}

                    orderPriceAdjusted = Math.Round(orderPrice + priceMargin, 2); // to get execution done ahead of anyone else for that bid/ask
                    errorCode = AlgoParams.Broker.PlaceDerivativeOrder(AlgoParams.I.Symbol,
                       AlgoParams.I.Qty,
                       isMarketOrder ? 0 : orderPriceAdjusted,
                       isMarketOrder ? OrderPriceType.MARKET : OrderPriceType.LIMIT,
                       orderDirection,
                       AlgoParams.I.InstrumentType,
                       AlgoParams.I.StrikePrice,
                       AlgoParams.I.ExpiryDate,
                       OrderGoodNessType.IOC,
                       out orderToPlace.OrderRef);
                }

                if (errorCode == BrokerErrorCode.NotLoggedIn)
                {
                    AlgoParams.Broker.LogOut();
                    errorCode = AlgoParams.Broker.CheckAndLogInIfNeeded(true);
                    continue;
                }
                if (errorCode == BrokerErrorCode.Success)
                    break;


                var errMsg = string.Format("ERROR {4} in PlaceOrder.{1} {2} qty of {0} at {3}", AlgoParams.I.ToString(),
            orderDirection, AlgoParams.I.Qty, orderPriceAdjusted, errorCode);
                FileTracing.TraceOut(errMsg);
                MessagingUtils.SendAlertMessage("Order Failed", errMsg);
                return false;
                // BUGBUGC: handle the exception, Insufficient funds etc. all
                //throw new Exception("BSA:PlaceOrder:ErrorCode=" + errorCode);
            }

            // --- Check order status and execution price

            if (!(AlgoParams.IsMock || AlgoParams.IsReplayMode))
                hasOrderExecuted = LoopUntilFutureOrderIsExecutedOrCancelled(orderToPlace.OrderRef, out executionPrice);
            else
                hasOrderExecuted = true;

            // Get Actual execution price
            if (hasOrderExecuted)
                orderToPlace.OrderPrice = executionPrice != -1 ? executionPrice : orderPrice;
            else
            {
                TotalFailedOrders++;
                FileTracing.TraceTradeInformation("Order not Executed");
            }

            return hasOrderExecuted;
        }
        protected bool LoopUntilFutureOrderIsExecutedOrCancelled(string orderReference, out double executionPrice)
        {
            executionPrice = -1;

            while (true)
            {
                DateTime fromDate = DateTime.Now;
                DateTime toDate = DateTime.Now;
                OrderStatus orderStatus;
                BrokerErrorCode errorCode = BrokerErrorCode.Success;

                Thread.Sleep(5 * 1000);
                // Incase of mock or testing replay
                if (AlgoParams.IsMock || AlgoParams.IsReplayMode)
                {
                    orderStatus = OrderStatus.EXECUTED;
                }
                else
                {
                    errorCode = AlgoParams.Broker.GetOrderStatus(orderReference,
                                            AlgoParams.I.InstrumentType, fromDate, toDate, out orderStatus);
                }

                // If fatal error then come out
                if (LoginUtils.IsErrorFatal(errorCode))
                {
                    FileTracing.TraceOut("PlaceOrder: Fatal Error " + errorCode.ToString());
                    return false;
                }

                // EXECUTED
                if (orderStatus.Equals(OrderStatus.EXECUTED))
                {
                    int retryCount = 0;

                    if (!(AlgoParams.IsMock || AlgoParams.IsReplayMode))
                    {
                        // Get actual execution price
                        do
                        {
                            executionPrice = AlgoParams.Broker.GetTradeExecutionPrice(fromDate, toDate, AlgoParams.I.InstrumentType, orderReference);
                        } while (executionPrice == -1 && retryCount++ <= 2);
                    }
                    return true;
                }


                if (orderStatus == OrderStatus.REJECTED ||
                    orderStatus == OrderStatus.CANCELLED ||
                    orderStatus == OrderStatus.EXPIRED)
                {
                    return false;
                }
            }
        }

        // ------- ITradingAlgo Interface methods -------- //
        public BrokerErrorCode Prolog()
        {
            AlgoWorkingState = AlgoOrderPlaceState.RUNNING;
            BrokerErrorCode errorCode = BrokerErrorCode.Success;
            string traceString = string.Format("TradingAlgo{0}.Prolog:: ENTER: {1} at {2}\n", AlgoParams.AlgoId, AlgoParams.ToString(), DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString());
            FileTracing.TraceOut(traceString);

            //if (!AlgoParams.IsMock)
            //{
            //    errorCode = AlgoParams.Broker.CheckAndLogInIfNeeded(false);
            //    if (errorCode != BrokerErrorCode.Success)
            //        FileTracing.TraceOut("ERROR: CheckAndLogInIfNeeded(). BrokerErrorCode=" + errorCode.ToString());
            //}
            try
            {
                // Read Algo state file
                if (File.Exists(AlgoParams.StateFile))
                {
                    using (FileStream stream = File.Open(AlgoParams.StateFile, FileMode.Open))
                    {
                        BinaryFormatter bformatter = new BinaryFormatter();
                        S = ((AlgoState)bformatter.Deserialize(stream));
                    }

                    // Map the Algo params from the State file
                    DoStopAlgo = S.DoStopAlgo;
                    AlgoWorkingState = S.AlgoWorkingState;
                    IsExternallySuspended = S.IsExternallySuspended;
                }

                // Process initial orders
                if (AlgoParams.StartOrders != null)
                    foreach (StockOrder order in AlgoParams.StartOrders)
                    {
                        if (order.OrderPosition != Position.NONE)
                        {
                            S.OpenPositions.Add(new StockOrder(order));
                            if (order.OrderPosition == Position.BUY)
                                S.TotalBuyTrades++;
                            else if (order.OrderPosition == Position.SELL)
                                S.TotalSellTrades++;
                        }
                    }

                // Read overnight or outside positions file
                if (File.Exists(AlgoParams.PositionsFile))
                {
                    int buys = 0, sells = 0;
                    using (StreamReader sr = new StreamReader(AlgoParams.PositionsFile))
                    {
                        string s = null;
                        do
                        {
                            s = sr.ReadLine();
                            if (s != null)
                            {
                                var parts = s.Split(':');
                                Position position = Position.NONE;
                                double price = 0;
                                if (double.TryParse(parts[1], out price) && Enum.TryParse(parts[0].ToUpper(), true, out position))
                                {
                                    StockOrder order = new StockOrder(position, price);
                                    // MGOYAL: BUGBUG
                                    // This is a hack to get AvgTradePrice to be non-zero in case of off-market run
                                    // and some initial orders being present
                                    mAverageTradePrice = price;
                                    S.OpenPositions.Add(order);

                                    if (position == Position.BUY)
                                        buys++;
                                    else if (position == Position.SELL)
                                        sells++;

                                }
                            }
                        } while (s != null);
                    }


                    if (buys > 0) S.TotalBuyTrades = buys;
                    if (sells > 0) S.TotalSellTrades = sells;

                    foreach (var position in S.OpenPositions)
                        FileTracing.TraceOut("InitialPosition = " + position);

                }
            }
            catch (IOException e)
            {
                Logger.LogException(e);
            }

            traceString = string.Format("TradingAlgo{0}.Prolog:: EXIT: {1}\n", AlgoParams.AlgoId, AlgoParams.ToString());
            FileTracing.TraceOut(traceString);

            ErrorCode = errorCode;
            return errorCode;
        }
        public BrokerErrorCode RunCoreAlgo()
        {
            BrokerErrorCode errorCode = BrokerErrorCode.Success;
            // replay bsa text file
            if (AlgoParams.IsReplayMode && !bReplayDone)
            {
                #region ReplayBSATicksFile
                bReplayDone = true;

                List<SymbolTick> sts = new List<SymbolTick>(10000);

                using (FileStream fs = new FileStream(AlgoParams.ReplayTickFile, FileMode.Open, FileAccess.Read))
                {
                    using (StreamReader sr = new StreamReader(fs))
                    {
                        string s = null;
                        do
                        {
                            s = sr.ReadLine();
                            if (s != null)
                            {
                                string[] parts = s.Split(';');

                                IFormatProvider culture = new CultureInfo("en-US", true);
                                DateTime dt = DateTime.ParseExact(parts[0], "yyyyMMdd:HH:mm:ss", culture);
                                int volumeTraded = 0;
                                if (parts.Length == 7)
                                {
                                    volumeTraded = int.Parse(parts[6]);
                                }

                                DerivativeSymbolQuote dsq = new DerivativeSymbolQuote(
                                    dt,
                                    double.Parse(parts[1]),
                                    double.Parse(parts[2]),
                                    double.Parse(parts[3]),
                                    int.Parse(parts[4]),
                                    int.Parse(parts[5]),
                                    volumeTraded
                                    );

                                var si = new SymbolTick(true);
                                si.I = AlgoParams.I;
                                si.D.Q = dsq;
                                sts.Add(si);

                                if (sts.Count > 100000)
                                    break;
                            }

                        } while (s != null);
                    }
                }

                // Run for each quote tick now out of all collected from the replay file
                foreach (SymbolTick si in sts)
                {
                    S.TotalTickCount++;
                    AddTick(si);
                    LTP = si.GetLTP();
                }


                #endregion
            }
            else if (!AlgoParams.IsReplayMode)
            {
                #region ActualAlgoLoop
                try
                {
                    DerivativeSymbolQuote dsq;
                    errorCode = AlgoParams.Broker.GetDerivativeQuote(AlgoParams.I.Symbol,
                         AlgoParams.I.InstrumentType,
                         AlgoParams.I.ExpiryDate,
                         0,
                         out dsq);

                    //dsq.UpdateTime = DateTime.Now;
                    var si = new SymbolTick(true);
                    si.I = AlgoParams.I;
                    si.D.Q = dsq;
                    if (errorCode.Equals(BrokerErrorCode.Success))
                    {
                        S.TotalTickCount++;
                        AddTick(si);
                        LTP = si.GetLTP();
                    }
                    else
                    {
                        FileTracing.TraceOut("StockTrader: " + errorCode.ToString());
                    }
                }
                catch
                {
                    FileTracing.TraceOut("Exception in algo.AddTick()");
                }
                #endregion
            }
            return errorCode;
        }
        public BrokerErrorCode RunCoreAlgoLive(SymbolTick si)
        {
            BrokerErrorCode errorCode = BrokerErrorCode.Success;

            try
            {
                S.TotalTickCount++;
                AddTick(si);
                LTP = si.GetLTP();
            }
            catch (Exception ex)
            {
                string msg = string.Format("Exception in algo.AddTick()\nError: {0}\n Stack:{1}", ex.Message, ex.StackTrace);
                FileTracing.TraceOut(msg);
            }

            return errorCode;
        }
        public BrokerErrorCode Epilog()
        {
            AlgoWorkingState = AlgoOrderPlaceState.FINISHED;
            BrokerErrorCode errorCode = BrokerErrorCode.Success;
            try
            {
                // Write the open positions back into file for the next session
                List<StockOrder> openPositions = GetCurrentOpenPositions();
                AlgoRunStats stats = GetEOPStats(LTP);
                ProfitLossStats plStats = stats.PnLStats;
                ProgramStats progStats = stats.ProgStats;

                _cummulativeProfitBooked += plStats.Profit;
                _nettFruitIfSquaredOffAtLTP = _cummulativeProfitBooked + plStats.Outstanding;

                if (!AlgoParams.IsReplayMode)
                    // Write back open positions file
                    using (StreamWriter sw = new StreamWriter(AlgoParams.PositionsFile))
                    {
                        foreach (StockOrder order in openPositions)
                        {
                            var str = order.OrderPosition + ";" + order.OrderPrice;
                            sw.WriteLine(str);
                        }
                    }

                // Main EOP calculations
                GetEOPStatsAndTrace(LTP);

                // Save a backup of the positions, algo state and other algo specific files
                if (!AlgoParams.IsReplayMode)
                {
                    string backupPath = SystemUtils.GetStockFilesBackupLocation();
                    string time = DateTime.Now.ToString("HHmm");
                    //string fullBackupPath = backupPath + "\\" + time + "\\";

                    // Dont do timewise
                    //fullBackupPath = backupPath;

                    if (!Directory.Exists(backupPath))
                        Directory.CreateDirectory(backupPath);

                    string bkupPositionsfile = Path.Combine(backupPath, Path.GetFileName(AlgoParams.PositionsFile));
                    string bkupStatefile = Path.Combine(backupPath, Path.GetFileName(AlgoParams.StateFile));
                    if (File.Exists(AlgoParams.PositionsFile) /* && !File.Exists(bkupPositionsfile) */) File.Copy(AlgoParams.PositionsFile, bkupPositionsfile, true);
                    if (File.Exists(AlgoParams.StateFile) /* && !File.Exists(bkupPositionsfile) */) File.Copy(AlgoParams.StateFile, bkupPositionsfile, true);

                }


                // Program stats
                StringBuilder sb = new StringBuilder("Total failed orders : " + progStats.FailedOrders + "\n");
                sb.AppendLine(string.Format("TradingAlgo{0}.Epilog:: {1}\n", AlgoParams.AlgoId, AlgoParams.ToString()));
                sb.AppendLine(" ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ ");
                FileTracing.TraceOut(sb.ToString());

                return errorCode;
            }
            catch (Exception ex) { Logger.LogException(ex); return errorCode; }
        }
        public int GetSleepTimeInMilliSecs()
        {
            return AlgoParams.AlgoIntervalInSeconds * 1000;
        }
        public string Description()
        {
            return AlgoParams.Description();
        }

        // ------- Remote Command methods -------- //
        public void Stop(bool doAlert = true)
        {
            DoStopAlgo = true;
            AlgoWorkingState = AlgoOrderPlaceState.STOPPED;
            string desc = string.Format("Stop Algo: {0}", AlgoParams.ToString());
            FileTracing.TraceInformation(desc);

            if (doAlert)
                MessagingUtils.SendAlertMessage("Stop", desc);
        }
        public void Pause(bool doAlert = true)
        {
            AlgoWorkingState = AlgoOrderPlaceState.PAUSED;

            string desc = string.Format("Pause Algo: {0} at {1}", AlgoParams.ToString(), DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString());
            FileTracing.TraceInformation(desc);

            if (doAlert)
                MessagingUtils.SendAlertMessage("Pause", desc);
        }
        public void PauseNewPos(bool doAlert = true)
        {
            AlgoWorkingState = AlgoOrderPlaceState.PAUSEDNEWPOS;

            string desc = string.Format("PauseNewPos Algo: {0}", AlgoParams.ToString());
            FileTracing.TraceInformation(desc);

            if (doAlert)
                MessagingUtils.SendAlertMessage("PauseNewPos", desc);
        }
        public void Resume(bool doAlert = true)
        {
            DoStopAlgo = false;
            AlgoWorkingState = AlgoOrderPlaceState.RUNNING;

            string desc = string.Format("Resume Algo: {0}", AlgoParams.ToString());
            FileTracing.TraceInformation(desc);

            if (doAlert)
                MessagingUtils.SendAlertMessage("Resume", desc);
        }
        public void OnRemoteCmd(string title, string desc, bool doAlert)
        {
            FileTracing.TraceInformation(desc);
            if (doAlert)
                MessagingUtils.SendAlertMessage(title, desc);
            PostProcAddTick();
        }

        protected void ResetRunState()
        {
            // Reset full means start the algo totally afresh
            AlgoWorkingState = AlgoOrderPlaceState.RUNNING;
            DoStopAlgo = false;
            IsExternallySuspended = false;
        }
        protected void UpdatePositionsOnReset()
        {
            if (S.TotalBuyTrades != S.TotalSellTrades)
            {
                int numPositions = S.TotalBuyTrades - S.TotalSellTrades;
                bool isBuy = numPositions < 0;
                var pos = isBuy ? Position.BUY : Position.SELL;
                var newOrder = new StockOrder(pos, S.CurrTick.Di.LastTradedPriceDouble);
                var squaredOffOrder = GetPairedSquareOffOrder(pos, true);
                if (squaredOffOrder == null) throw new Exception("No matching squareoff order found");
                AfterSquareOff(newOrder, squaredOffOrder);
                _lastPeak = S.CurrTick;
            }
        }
        public void ResetFull(bool doAlert = true)
        {
            ErrorCode = BrokerErrorCode.Success;
            mAverageTradePrice = 0;
            _eodTradeStats = new EODTradeStats(AlgoParams.R1, AlgoParams.R2);
            _eopTradeStats = new EOPTradeStats(AlgoParams.R1, AlgoParams.R2);

            S = new AlgoState();

            S.PercChangeThreshold = AlgoParams.PercMarketDirectionChange;

            // Reset full means start the algo totally afresh
            ResetRunState();

            string desc = string.Format("Reset Full Algo: {0}", AlgoParams.ToString());
            desc += StockUtils.CollateAlgoStates(new TradingAlgo[] { this });
            OnRemoteCmd("Reset", desc, doAlert);
        }
        public void ResetCore(bool doAlert = true)
        {
            UpdatePositionsOnReset();

            S.PercChangeThreshold = AlgoParams.PercMarketDirectionChange;

            S.IsOnceCrossedMinProfit = false;
            S.IsMinProfitDirectionPercRaised = false;
            S.LastPeakType = PeakType.NONE;

            S.MinTick = S.CurrTick;
            S.MaxTick = S.CurrTick;

            string desc = string.Format("Reset Core State Algo: {0}", AlgoParams.ToString());
            desc += StockUtils.CollateAlgoStates(new TradingAlgo[] { this });
            OnRemoteCmd("Reset", desc, doAlert);
        }
        public void ResetDir(bool doAlert = true)
        {
            UpdatePositionsOnReset();

            S.IsOnceCrossedMinProfit = false;
            S.IsMinProfitDirectionPercRaised = false;
            S.PercChangeThreshold = AlgoParams.PercMarketDirectionChange;

            S.LastPeakType = PeakType.NONE;

            string desc = string.Format("Reset PositionsAndDirection Algo: {0}", AlgoParams.ToString());
            desc += StockUtils.CollateAlgoStates(new TradingAlgo[] { this });
            OnRemoteCmd("Reset", desc, doAlert);
        }
        public void ResetPos(bool doAlert = true)
        {
            UpdatePositionsOnReset();

            S.IsOnceCrossedMinProfit = false;
            S.IsMinProfitDirectionPercRaised = false;
            S.PercChangeThreshold = AlgoParams.PercMarketDirectionChange;

            string desc = string.Format("Reset Positions Algo: {0}", AlgoParams.ToString());
            desc += StockUtils.CollateAlgoStates(new TradingAlgo[] { this });
            OnRemoteCmd("Reset", desc, doAlert);
        }
        protected OrderPlaceAlgoCode TryImmediateSquareOff(bool isContinueTillSuccess, bool placeMarketOrder, bool passThroughOrder)
        {
            int numOpenPositions = S.TotalBuyTrades - S.TotalSellTrades;
            OrderPlaceAlgoCode orderPlaceCode = OrderPlaceAlgoCode.SUCCESS;
            while (numOpenPositions != 0)
            {
                bool isBuy = numOpenPositions < 0;

                orderPlaceCode = AlgoTryPlaceOrder(isBuy, S.CurrTick, true, placeMarketOrder, passThroughOrder); // Place market order

                if (orderPlaceCode == OrderPlaceAlgoCode.SUCCESS)
                {
                    if (!isBuy)
                    {
                        _lastPeak = S.MaxTick;
                        S.MinTick = S.CurrTick;
                    }
                    else
                    {
                        _lastPeak = S.MinTick;
                        S.MaxTick = S.CurrTick;
                    }
                    S.LastPeakType = PeakType.NONE;
                    S.IsOnceCrossedMinProfit = false;
                    S.IsMinProfitDirectionPercRaised = false;

                    // Reset Algo Variables for safety
                    UpdateMinMax();
                    S.PercChangeThreshold = AlgoParams.PercMarketDirectionChange;

                    _allPeaks.Add(S.TotalTickCount, _lastPeak);
                }
                else if (orderPlaceCode != OrderPlaceAlgoCode.RETRY)
                    break;

                if (!isContinueTillSuccess)
                    break;

                Thread.Sleep(1000 * 2);
                numOpenPositions = S.TotalBuyTrades - S.TotalSellTrades;
            }

            return orderPlaceCode;
        }
        public OrderPlaceAlgoCode SquareOff(bool doAlert = true, bool placeMarketOrder = true)
        {
            //var state = AlgoWorkingState;
            //AlgoWorkingState = AlgoOrderPlaceState.RUNNING;
            var orderPlaceCode = TryImmediateSquareOff(true, placeMarketOrder, true);
            //AlgoWorkingState = state;
            string desc = string.Format("Algo {0}: SquaredOff Positions, Status:{1}", AlgoParams.ToString(), orderPlaceCode);
            OnRemoteCmd("SquareOff", desc, doAlert);

            return orderPlaceCode;
        }
        public void SquareOffPause(bool doAlert = true)
        {
            var orderPlaceCode = SquareOff(false);
            Pause(false);

            string desc = string.Format("Algo {0}: SquaredOff Positions, Status:{1} and Paused", AlgoParams.ToString(), orderPlaceCode);
            OnRemoteCmd("SquareOffPause", desc, doAlert);
        }
        public bool SquareOffAtProfit(bool doAlert = true)
        {
            int numOpenPositions = S.TotalBuyTrades - S.TotalSellTrades;
            if (numOpenPositions != 0)
                S.IsSquareoffAtProfit = true;

            var status = numOpenPositions != 0 ? "Set" : " No open positions to SquareOff";
            string desc = string.Format("Algo {0}: SquareOffAtProfit, Status:{1}", AlgoParams.ToString(), status);
            OnRemoteCmd("SquareOffAtProfit", desc, doAlert);

            return numOpenPositions != 0;
        }
        public void SquareOffAtProfitPause(bool doAlert = true)
        {
            var isOpenPos = SquareOffAtProfit(false);

            if (isOpenPos)
                S.IsPauseAfterSquareOffAtProfit = true;

            var status = isOpenPos ? "Set" : " No open positions to SquareOff";
            string desc = string.Format("Algo {0}: SquaredOffAtProfit, Status:{1} and Paused", AlgoParams.ToString(), status);
            OnRemoteCmd("SquareOffAtProfitPause", desc, doAlert);
        }

        // ------- Tracing tradeSummary  methods -------- //
        protected List<StockOrder> GetCurrentOpenPositions()
        {
            List<StockOrder> list = new List<StockOrder>();
            foreach (StockOrder o in S.OpenPositions)
                list.Add(new StockOrder(o));
            return list;
        }
        public AlgoRunStats GetEOPStats(double ltp)
        {
            AlgoRunStats stats = new AlgoRunStats();
            ProfitLossStats plStats = stats.PnLStats;
            ProgramStats progStats = stats.ProgStats;

            plStats.BuyTrades = S.TotalBuyTrades;
            plStats.SellTrades = S.TotalSellTrades;
            plStats.Brokerage = S.TotalBrokerageAmt;
            plStats.Profit = TotalActualNettProfitAmt;

            // Open positions
            double avgBuyPrice = 0, avgSellPrice = 0;
            int buyPos = 0, sellPos = 0;

            foreach (StockOrder order in GetCurrentOpenPositions())
            {
                if (order.OrderPosition == Position.BUY)
                {
                    avgBuyPrice += order.OrderPrice;
                    buyPos++;
                }
                else if (order.OrderPosition == Position.SELL)
                {
                    avgSellPrice += order.OrderPrice;
                    sellPos++;
                }
            }

            avgBuyPrice /= (buyPos > 0) ? buyPos : 1;
            avgSellPrice /= (sellPos > 0) ? sellPos : 1;

            int loop = Math.Min(buyPos, sellPos);

            buyPos -= loop;
            sellPos -= loop;

            double nettOpenPositionPoints = (avgSellPrice - avgBuyPrice) * loop;
            if (buyPos > sellPos)
                nettOpenPositionPoints += (ltp - avgBuyPrice) * buyPos;
            else
                nettOpenPositionPoints += (avgSellPrice - ltp) * sellPos;

            double brokeragePoints = (AlgoParams.PercBrokerage * ltp * (AlgoParams.SquareOffBrokerageFactor * loop + Math.Abs(buyPos - sellPos))) / 100;
            nettOpenPositionPoints -= brokeragePoints;
            double nettOpenPositionAmt = nettOpenPositionPoints * AlgoParams.I.Qty;
            plStats.Outstanding = nettOpenPositionAmt;
            double nettDay = TotalActualNettProfitAmt + nettOpenPositionAmt;
            plStats.NettAfterSquareOffAtClosingPrice = nettDay;

            progStats.FailedOrders = TotalFailedOrders;
            return stats;
        }
        public void GetEOPStatsAndTrace(double ltp)
        {
            mAverageTradePrice = S.TotalTickCount == 0 ? 0 : mAverageTradePrice / S.TotalTickCount;

            ltp = ltp != 0 ? ltp : mAverageTradePrice != 0 ? mAverageTradePrice : 1;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("");
            sb.AppendLine(" ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ ");
            //sb.AppendLine(string.Format("IsDoubleRide = {0}", mIsDoubleRide));
            sb.AppendLine(string.Format("Algo = {0}", AlgoParams.AlgoId));
            sb.AppendLine(string.Format("IsMarketClosingSquareOff = {0}", AlgoParams.IsMarketClosingSquareOff));
            sb.AppendLine(string.Format("MinProfitPercentageIfSquareOffEnabled(Enabled={0}) = {1}", AlgoParams.IsMinProfitMust, AlgoParams.PercMinProfit));
            sb.AppendLine(string.Format("SquareOffThreshold(Enabled={0}) = {1}", AlgoParams.IsSquareOffTrigger, AlgoParams.PercSquareOffThreshold));
            sb.AppendLine(string.Format("MarketDirectionThreshold = {0}", AlgoParams.PercMarketDirectionChange));
            sb.AppendLine(string.Format("BrokerageRate = {0}", AlgoParams.PercBrokerage));
            sb.AppendLine(" \n-----------------------------------------------\n");
            sb.AppendLine(string.Format("Trades: Buy = {0} Sell = {1}", S.TotalBuyTrades, S.TotalSellTrades));
            sb.AppendLine(string.Format("Trades: Profitable = {0} Profitable(AfterBrokerage) = {1} Loss = {2}",
                _profitableOrders.Count, _nettProfitableOrders.Count, _lossOrders.Count));
            sb.AppendLine("Total Trade Diff. amount (after brokerage) : " + TotalActualNettProfitAmt);
            sb.AppendLine("Total Trade Diff. amount (after brokerage) %: " + (TotalActualNettProfitAmt / (ltp * AlgoParams.I.Qty)) * 100);

            double TotalProfitWithoutBrokerage = TotalActualNettProfitAmt + S.TotalBrokerageAmt;

            sb.AppendLine("Open Positions");
            double avgBuyPrice = 0, avgSellPrice = 0;
            int buyPos = 0, sellPos = 0;

            // Open positions
            foreach (StockOrder order in GetCurrentOpenPositions())
            {
                sb.AppendLine("OrderDetails: " + order);
                if (order.OrderPosition == Position.BUY)
                {
                    avgBuyPrice += order.OrderPrice;
                    buyPos++;
                }
                else if (order.OrderPosition == Position.SELL)
                {
                    avgSellPrice += order.OrderPrice;
                    sellPos++;
                }
            }

            avgBuyPrice /= (buyPos > 0) ? buyPos : 1;
            avgSellPrice /= (sellPos > 0) ? sellPos : 1;

            int loop = Math.Min(buyPos, sellPos);

            buyPos -= loop;
            sellPos -= loop;

            double nettOpenPositionPoints = (avgSellPrice - avgBuyPrice) * loop;
            if (buyPos > sellPos)
            {
                nettOpenPositionPoints += (ltp - avgBuyPrice) * buyPos;
            }
            else
            {
                nettOpenPositionPoints += (avgSellPrice - ltp) * sellPos;
            }
            double openPosBrokeragePoints = (AlgoParams.PercBrokerage / 100) * ltp * (AlgoParams.SquareOffBrokerageFactor * loop + Math.Abs(buyPos - sellPos));
            double openPosBrokerageAmt = openPosBrokeragePoints * AlgoParams.I.Qty;
            nettOpenPositionPoints -= openPosBrokeragePoints;
            sb.AppendLine("Outstanding Trade Diff. points (after brokerage) : " + nettOpenPositionPoints);
            sb.AppendLine("LTP : " + ltp);
            double nettOpenPositionAmt = nettOpenPositionPoints * AlgoParams.I.Qty;
            sb.AppendLine("Nett outstanding Amount (after brokerage) : " + nettOpenPositionAmt);
            double nettDay = TotalActualNettProfitAmt + nettOpenPositionAmt;
            sb.AppendLine("Nett fruit points (if squared off at LTP's Trade Diff.) : " + nettDay / AlgoParams.I.Qty);
            int marginPositions = Math.Max(AlgoParams.MaxLongPositions, AlgoParams.MaxShortPositions);
            double moneyInvested = AlgoParams.I.Qty * marginPositions * ltp * AlgoParams.MarginFraction;

            sb.AppendLine("\n-----------------------------------------------\n");
            sb.AppendLine("Money Invested : " + moneyInvested);
            TimeSpan tradeTimeWindow = TimeOfLastTickOfPeriod - TimeOfFirstTickOfPeriod;
            double numMonths = tradeTimeWindow.TotalHours / (24 * 30);
            numMonths = numMonths == 0 ? 1 : numMonths;
            sb.AppendLine(string.Format("StartTime: {0} EndTime:{1} NumDays:{2}", TimeOfFirstTickOfPeriod.ToShortDateString(),
                TimeOfLastTickOfPeriod.ToShortDateString(), numMonths * 30));

            string eachDayNett = "";
            foreach (DayNett nett in _dailyRunningNett)
            {
                eachDayNett += nett.Nett + ",";
            }
            sb.AppendLine("Days Earnings: " + eachDayNett);

            string expectedEachDayNett = "";
            foreach (DayNett nett in _dailyRunningExpectedNett)
            {
                expectedEachDayNett += nett.Nett + ",";
            }
            sb.AppendLine("Days Expected Earnings: " + expectedEachDayNett);

            sb.AppendLine("");
            double nettFruitWithoutBrokerage = TotalProfitWithoutBrokerage + nettOpenPositionAmt + openPosBrokerageAmt;
            sb.AppendLine(" ******************************************************************* ");
            sb.AppendLine("");
            //sb.AppendLine("Total Profit (without open pos squareoff after brokerage) : " + profitAmt);
            sb.AppendLine("Nett fruit amount (without brokerage) : " + nettFruitWithoutBrokerage);
            sb.AppendLine("Nett fruit amount (without brokerage) %: " + (nettFruitWithoutBrokerage * 100) / moneyInvested);
            sb.AppendLine("");
            sb.AppendLine("Nett Brokerage : " + S.TotalBrokerageAmt + openPosBrokerageAmt);
            double expectedNettDay = TotalExpectedNettProfitAmt + nettOpenPositionAmt;
            sb.AppendLine("Nett expected fruit amount : " + expectedNettDay);
            sb.AppendLine("Nett fruit amount : " + nettDay);
            sb.AppendLine("");
            sb.AppendLine("");

            sb.AppendLine("Nett Expected ROI percentage made : " + (100 * expectedNettDay) / moneyInvested);

            double nettPerc = (100 * nettDay) / moneyInvested;
            sb.AppendLine("Nett Actual ROI percentage made : " + nettPerc);
            sb.AppendLine("Nett Actual ROI percentage made (per month) : " + nettPerc / numMonths);

            sb.AppendLine("");

            sb.AppendLine(" ******************************************************************* ");
            FileTracing.TraceInformation(sb.ToString());

            if (!AlgoParams.IsMarketClosingSquareOff)
            {
                _eopTradeStats.number_of_ticks = S.TotalTickCount;
                _eopTradeStats.num_days = (int)((TimeOfLastTickOfPeriod.Date - TimeOfFirstTickOfPeriod.Date).TotalDays * 0.7);
                _eopTradeStats.inmarket_time_in_minutes = (int)(_eopTradeStats.num_days * 60 * 5.5);
            }

            // Persist end of period trade stats into DB
            _eopTradeStats.start_date = TimeOfFirstTickOfPeriod.Date;
            _eopTradeStats.end_date = TimeOfLastTickOfPeriod.Date;

            // Using Min because we may not be able to squareoff last trade. If last day's MarketClosing ticks not present in data
            _eopTradeStats.num_trades = Math.Min(S.TotalBuyTrades, S.TotalSellTrades);
            _eopTradeStats.roi_percentage = ((TotalExpectedNettProfitAmt * 100) / ((mAverageTradePrice == 0 ? 1 : mAverageTradePrice) * AlgoParams.I.Qty)) * (1 / AlgoParams.MarginFraction);
            _eopTradeStats.status_update_time = DateTime.Now;
            _eopTradeStats.market_direction_percentage = AlgoParams.PercMarketDirectionChange;
            _eopTradeStats.average_trade_price = mAverageTradePrice;
            _eopTradeStats.expected_profit = TotalExpectedNettProfitAmt;
            _eopTradeStats.actual_profit = TotalActualNettProfitAmt;
            _eopTradeStats.brokerage = S.TotalBrokerageAmt;
            _eopTradeStats.quantity = AlgoParams.I.Qty;
            _eopTradeStats.actual_roi_percentage = ((TotalActualNettProfitAmt * 100) / ((mAverageTradePrice == 0 ? 1 : mAverageTradePrice) * AlgoParams.I.Qty)) * (1 / AlgoParams.MarginFraction);
            _eopTradeStats.contract_name = AlgoParams.I.Symbol;
            _eopTradeStats.algo_id = AlgoParams.AlgoId;
            _eopTradeStats.average_profit_pertrade = _eopTradeStats.num_profit_trades == 0 ? 0 : _eopTradeStats.average_profit_pertrade / _eopTradeStats.num_profit_trades;
            _eopTradeStats.average_loss_pertrade = _eopTradeStats.num_loss_trades == 0 ? 0 : _eopTradeStats.average_loss_pertrade / _eopTradeStats.num_loss_trades;

            //EopTradeStats.num_days = (int)(mTimeOfLastTick - mTimeOfFirstTick).TotalDays;
            //EopTradeStats.inmarket_time_in_minutes = 0;
            //EopTradeStats.number_of_ticks = S.TotalTickCount;
            if (S.TotalTickCount > 0)
                _eopTradeStats.Persist();
        }

        #region Windows APIs

        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern Boolean Beep(UInt32 frequency, UInt32 duration);

        #endregion
    }
}
