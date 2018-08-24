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
    public abstract partial class TradingAlgoHedge : TradingAlgo
    {
        protected TradingAlgoHedge(AlgoParams Params)
            : base(Params)
        {

        }

        public double GetSquareOffProfitPerc(bool considerHedge = false)
        {
            double profitPerc = 0;
            try
            {
                bool isBuy = S.TotalBuyTrades < S.TotalSellTrades;

                // fut
                double orderPrice = isBuy ? S.Ticks.CurrTick.Di.BestOfferPriceDouble : S.Ticks.CurrTick.Di.BestBidPriceDouble;
                double profitPoints = orderPrice - GetPairedSquareOffOrder(isBuy ? Position.BUY : Position.SELL, DerivativePositionType.Future, false).OrderPrice; // if sqroff done outside then sometimes this may lead to wrong state
                profitPoints = isBuy ? -profitPoints : profitPoints;
                double brokerage = (AlgoParams.SquareOffBrokerageFactor * AlgoParams.PercBrokerage * orderPrice / 100);
                double nettProfit = profitPoints - brokerage;

                if (considerHedge)
                {
                    // call
                    var sqrOffOrd = GetPairedSquareOffOrder(isBuy ? Position.BUY : Position.SELL, DerivativePositionType.Call, false);
                    if (sqrOffOrd != null)
                    {
                        double orderPrice1 = isBuy ? S.TickCall.Di.BestOfferPriceDouble : S.TickCall.Di.BestBidPriceDouble;
                        double profitPoints1 = orderPrice1 - sqrOffOrd.OrderPrice; // if sqroff done outside then sometimes this may lead to wrong state
                        profitPoints1 = isBuy ? -profitPoints1 : profitPoints1;
                        double brokerage1 = AlgoParams.OptionsIntradayBrokerage / AlgoParams.I.Qty;
                        nettProfit += (profitPoints1 - brokerage1);
                    }

                    // put
                    sqrOffOrd = GetPairedSquareOffOrder(isBuy ? Position.BUY : Position.SELL, DerivativePositionType.Put, false);
                    if (sqrOffOrd != null)
                    {
                        double orderPrice2 = isBuy ? S.TickPut.Di.BestOfferPriceDouble : S.TickPut.Di.BestBidPriceDouble;
                        double profitPoints2 = orderPrice2 - sqrOffOrd.OrderPrice; // if sqroff done outside then sometimes this may lead to wrong state
                        profitPoints2 = isBuy ? -profitPoints2 : profitPoints2;
                        double brokerage2 = AlgoParams.OptionsIntradayBrokerage / AlgoParams.I.Qty;
                        nettProfit += (profitPoints2 - brokerage2);
                    }
                }
                profitPerc = (100 * nettProfit) / orderPrice;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
            return profitPerc;
        }
        public override StockOrder GetOpenPosition() // make it handle call/put positions also later
        {
            if (S.TotalBuyTrades != S.TotalSellTrades)
                return S.OpenPositions[0];

            return null;
        }
        public bool IsOpenPosition(DerivativePositionType posType) // make it handle call/put positions also later
        {
            if (S.TotalBuyTrades != S.TotalSellTrades)
            {
                if (posType == DerivativePositionType.Call)
                    return S.OpenPositionsCall.Count > 0;
                else if (posType == DerivativePositionType.Put)
                    return S.OpenPositionsPut.Count > 0;
                else if (posType == DerivativePositionType.Future)
                    return S.OpenPositions.Count > 0;
            }
            return false;
        }
        protected StockOrder GetPairedSquareOffOrder(Position pos, DerivativePositionType posType, bool isRemove)
        {
            StockOrder squaredOffOrder = null;
            var positions = S.OpenPositions;

            if (posType == DerivativePositionType.Call)
                positions = S.OpenPositionsCall;
            else if (posType == DerivativePositionType.Put)
                positions = S.OpenPositionsPut;

            for (int i = positions.Count - 1; i >= 0; i--)
            {
                if (positions[i].OrderPosition != pos)
                {
                    squaredOffOrder = positions[i];
                    if (isRemove)
                        positions.RemoveAt(i);
                    break;
                }
            }
            return squaredOffOrder;
        }
        protected List<StockOrder> GetPairedSquareOffOrders(Position pos, Position optPos, bool isRemove)
        {
            List<StockOrder> squaredOffOrders = new List<StockOrder>(3);
            squaredOffOrders.Add(GetPairedSquareOffOrder(pos, DerivativePositionType.Future, isRemove));
            var sqrfOffOrd = GetPairedSquareOffOrder(optPos, DerivativePositionType.Call, isRemove);
            if (sqrfOffOrd != null)
                squaredOffOrders.Add(sqrfOffOrd);
            sqrfOffOrd = GetPairedSquareOffOrder(optPos, DerivativePositionType.Put, isRemove);
            if (sqrfOffOrd != null)
                squaredOffOrders.Add(sqrfOffOrd);
            return squaredOffOrders;
        }
        //public DTick GetNearestOptionTick(bool isBuy, DerivativePositionType posType, double strkPrc)
        //{
        //    S.TickCall = StockUtils.GetNearestOptionTick(S.Ticks.CurrTick.Di.QuoteTime, S.TicksCall, strkPrc);
        //}

        // ------- ITradingAlgo Interface methods -------- later make them handle call/put positions also//
        public override BrokerErrorCode Prolog()
        {
            AlgoWorkingState = AlgoOrderPlaceState.RUNNING;
            BrokerErrorCode errorCode = BrokerErrorCode.Success;
            string traceString = string.Format("TradingAlgo{0}.Prolog:: ENTER: {1} at {2}\n", AlgoParams.AlgoId, AlgoParams.ToString(), DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString());
            FileTracing.TraceOut(traceString);

            try
            {
                // TODO: _eod/_eop getting reset (in constructor) on re-run. Not getting picked up same as last saved.
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
                    IsOrderExecutionPending = S.IsOrderExecutionPending;
                }

                // Process initial orders
                // fut
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
                // call
                if (AlgoParams.StartOrdersCall != null)
                    foreach (StockOrder order in AlgoParams.StartOrdersCall)
                        if (order.OrderPosition != Position.NONE)
                            S.OpenPositionsCall.Add(new StockOrder(order));
                // opt
                if (AlgoParams.StartOrdersPut != null)
                    foreach (StockOrder order in AlgoParams.StartOrdersPut)
                        if (order.OrderPosition != Position.NONE)
                            S.OpenPositionsPut.Add(new StockOrder(order));

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
                                if (double.TryParse(parts[1], out price)
                                    && Enum.TryParse(parts[0].ToUpper(), true, out position))
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
                                //double strikePrice1 = 0;
                                //double price1 = 0;
                                //if (double.TryParse(parts[2], out strikePrice1) && double.TryParse(parts[3], out price1))
                                //{
                                //    StockOrder order = new StockOrder(position, price1);
                                //    order.OrderTick = new DTick();
                                //    order.OrderTick.Di = new DerivativeSymbolQuote();
                                //    order.OrderTick.Di.StrikePriceDouble = strikePrice1;
                                //    S.OpenPositionsCall.Add(order);
                                //}
                                //double strikePrice2 = 0;
                                //double price2 = 0;
                                //if (double.TryParse(parts[4], out strikePrice2) && double.TryParse(parts[5], out price2))
                                //{
                                //    StockOrder order = new StockOrder(position, price2);
                                //    order.OrderTick = new DTick();
                                //    order.OrderTick.Di = new DerivativeSymbolQuote();
                                //    order.OrderTick.Di.StrikePriceDouble = strikePrice2;
                                //    S.OpenPositionsPut.Add(order);
                                //}

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
        public override BrokerErrorCode Epilog()
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
                    if (File.Exists(AlgoParams.StateFile) /* && !File.Exists(bkupPositionsfile) */) File.Copy(AlgoParams.StateFile, bkupStatefile, true);

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

        // ------- Core Algo methods start -------- //
        protected override bool FindPeaksAndOrder()
        {
            OrderPlaceAlgoCode orderPlaceCode = OrderPlaceAlgoCode.ALGOLIMITREJECT;
            // 1. CHECK NEW TOP (Always occur for square off trade only)
            if (S.LastPeakType == PeakType.BOTTOM)
            {
                if (S.Ticks.PercChangeFromMax < 0 && Math.Abs(S.Ticks.PercChangeFromMax) >= S.PercChangeThreshold)
                {
                    // Record tick abberations
                    if (Math.Abs(S.Ticks.PercChangeFromMax) - S.PercChangeThreshold > 1)
                    {
                        Logger.LogWarningMessage(
                             string.Format("{0}: At {1} : PriceTick non-continuous {4} times. PercThreshold = {2} S.Ticks.PercChangeFromMax = {3}.",
                                            AlgoParams.I.Symbol, S.Ticks.CurrTick.Di.QuoteTime, S.PercChangeThreshold, S.Ticks.PercChangeFromMax, S.Ticks.PercChangeFromMax / S.PercChangeThreshold));
                    }

                    orderPlaceCode = AlgoTryPlaceOrder(false, S.Ticks.CurrTick); // Sell

                    if (orderPlaceCode == OrderPlaceAlgoCode.SUCCESS)
                    {
                        // New TOP found
                        S.LastPeakType = PeakType.TOP;
                        S.Ticks.ResetMinMaxAndGetLastPeak(false, out _lastPeak);
                    }
                }
            }
            // 2. CHECK NEW BOTTOM (Always occur for square off trade only)
            else if (S.LastPeakType == PeakType.TOP)
            {
                // Peak - New trades or pending squareoffs
                if (S.Ticks.PercChangeFromMin > 0 && Math.Abs(S.Ticks.PercChangeFromMin) >= S.PercChangeThreshold)
                {
                    // Record tick abberations
                    if (Math.Abs(S.Ticks.PercChangeFromMin) - S.PercChangeThreshold > 1)
                    {
                        Logger.LogWarningMessage(
                             string.Format("{0}: At {1} : PriceTick non-continuous {4} times. PercThreshold = {2} S.Ticks.PercChangeFromMin = {3}.",
                                            AlgoParams.I.Symbol, S.Ticks.CurrTick.Di.QuoteTime, S.PercChangeThreshold, S.Ticks.PercChangeFromMin, S.Ticks.PercChangeFromMin / S.PercChangeThreshold));
                    }

                    orderPlaceCode = AlgoTryPlaceOrder(true, S.Ticks.CurrTick); // Buy

                    if (orderPlaceCode == OrderPlaceAlgoCode.SUCCESS)
                    {
                        // New BOTTTOM found
                        S.LastPeakType = PeakType.BOTTOM;
                        S.Ticks.ResetMinMaxAndGetLastPeak(true, out _lastPeak);
                    }

                }
            }
            // 3. 1st or FRESH position PEAK (TOP/BOTTOM)  (Never occurs for square off trade and always occur for fresh trade)
            else if (S.LastPeakType == PeakType.NONE)
            {
                bool isBuy = S.Ticks.PercChangeFromMin > 0 && Math.Abs(S.Ticks.PercChangeFromMin) >= S.PercChangeThreshold;
                bool isSell = S.Ticks.PercChangeFromMax < 0 && Math.Abs(S.Ticks.PercChangeFromMax) >= S.PercChangeThreshold;

                if (isBuy && isSell)
                {
                    // May occur due to order not getting executed. But in offline analysis should not occur ideally 
                    var msg = string.Format("Error: NONE peak says both buy and sell." +
                        "Some bug. Overcome is assume buy. max={0}, min={1}, th={2}", S.Ticks.PercChangeFromMax, S.Ticks.PercChangeFromMin, S.PercChangeThreshold);
                    FileTracing.TraceOut(msg);
                    isBuy = Math.Abs(S.Ticks.PercChangeFromMin) > Math.Abs(S.Ticks.PercChangeFromMax);
                    isSell = !isBuy;
                }
                if (isBuy || isSell)
                {
                    orderPlaceCode = AlgoTryPlaceOrder(isBuy, S.Ticks.CurrTick); // buy or sell

                    if (orderPlaceCode == OrderPlaceAlgoCode.SUCCESS)
                    {
                        // Set last peak as reverse of current tick direction
                        S.LastPeakType = isBuy ? PeakType.BOTTOM : PeakType.TOP;

                        if (S.LastPeakType == PeakType.BOTTOM)
                        {
                            // 1st peak is BOTTOM
                            _lastPeak = S.Ticks.MinTick;
                        }
                        else
                        {
                            // 1st peak is TOP
                            _lastPeak = S.Ticks.MaxTick;
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
            else if (orderPlaceCode == OrderPlaceAlgoCode.RETRY)
                S.IsOrderExecutionPending = true;

            return orderPlaceCode == OrderPlaceAlgoCode.SUCCESS;
        }
        protected override OrderPlaceAlgoCode AlgoTryPlaceOrder(bool isBuy, DTick tick, bool doNoPriceCheck = false
            , bool isMarketOrder = false, bool passThroughOrder = false)
        {
            // Check on Algo order placement state 
            //if (!isMarketOrder || )
            if (!passThroughOrder)
            {
                if (AlgoWorkingState != AlgoOrderPlaceState.RUNNING)
                {
                    // New positions are paused
                    if (AlgoWorkingState == AlgoOrderPlaceState.PAUSEDNEWPOS && !IsSquareOff(isBuy))
                        return OrderPlaceAlgoCode.ALGOPAUSEREJECT;

                        // Ifotheriwse paused
                    else if (AlgoWorkingState != AlgoOrderPlaceState.PAUSEDNEWPOS)
                        return OrderPlaceAlgoCode.ALGOPAUSEREJECT;
                }

                // Do not take new positions after 3.15
                if (MarketUtils.IsTimeAfter315(S.Ticks.CurrTick.Di.UpdateTime) && !IsSquareOff(isBuy))
                    return OrderPlaceAlgoCode.ALGORISKREJECT;
            }

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
                    // Just to optimize offline analysis
                    if (AlgoParams.IsMock || AlgoParams.IsReplayMode)
                    {
                        DoStopAlgo = true;
                        DoEODStatsWindup();
                    }
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
                    if (S.TotalActualNettProfitAmt < 0)
                    {
                        var percLoss = Math.Abs((S.TotalActualNettProfitAmt * 100) / (S.Ticks.MinTick.Di.LastTradedPriceDouble * AlgoParams.I.Qty));

                        // 1 & 2 are new conditiosn when running 46, 47 and 48
                        bool isStopLossHit1 = (percLoss >= AlgoParams.PercLossStopForDay * 0.75) && _eodTradeStats.num_loss_trades >= AlgoParams.NumNettLossTradesStopForDay;
                        bool isStopLossHit2 = (percLoss >= AlgoParams.PercLossStopForDay * 0.75) && _eodTradeStats.num_loss_trades >= 2 && _eodTradeStats.num_profit_trades == 0;
                        bool isStopLossHit3 = _eodTradeStats.num_loss_trades - _eodTradeStats.num_profit_trades >= AlgoParams.NumNettLossTradesStopForDay;
                        bool isStopLossHit4 = percLoss >= AlgoParams.PercLossStopForDay;

                        if (isStopLossHit4 || isStopLossHit3)//isStopLossHit1 || isStopLossHit2)
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
                            {
                                DoStopAlgo = true;
                                DoEODStatsWindup();
                            }

                            FileTracing.TraceOut(info);
                            return OrderPlaceAlgoCode.ALGORISKREJECT;
                        }
                    }
                }
            }

            var orderDirection = isBuy ? Position.BUY : Position.SELL;
            var isSqrOff = posCode == PositionAllowAlgoCode.ALLOWSQUAREOFF;
            var OptOrdDirection = isSqrOff ? Position.SELL : Position.BUY;

            Semaphore sem = new Semaphore(0, S.OrderTypesPendingCount);

            int ordersPendingExecution = S.OrderTypesPendingCount;

            // fut
            if ((S.OrdersExecuted & DerivativePositionType.Future) != DerivativePositionType.Future)
            {
                S.OrderParamsFut = new PlaceOrderParams(di, doNoPriceCheck, isMarketOrder, 0.2, DerivativePositionType.Future, sem);
                var orderParams = S.OrderParamsFut;

                orderParams.OrderToPlace = new StockOrder(orderDirection, 0, S.Ticks.CurrTick);
                orderParams.Instr = AlgoParams.I.Clone();
                new Thread(new ParameterizedThreadStart(PlaceOrder)).Start(orderParams);
            }

            // call
            if (posCode == PositionAllowAlgoCode.ALLOWNEWPOS || 
                (posCode == PositionAllowAlgoCode.ALLOWSQUAREOFF && IsOpenPosition(DerivativePositionType.Call)))
            if ((S.OrdersExecuted & DerivativePositionType.Call) != DerivativePositionType.Call)
            {
                S.OrderParamsCall = new PlaceOrderParams(null, doNoPriceCheck, isMarketOrder, 10, DerivativePositionType.Call, sem);
                var orderParams1 = S.OrderParamsCall;
                var instr1 = orderParams1.Instr = AlgoParams.I.Clone();
                instr1.InstrumentType = instr1.InstrumentType == InstrumentType.FutureIndex ? InstrumentType.OptionCallIndex : InstrumentType.OptionCallStock;
                if (posCode == PositionAllowAlgoCode.ALLOWSQUAREOFF)
                    instr1.StrikePrice = GetPairedSquareOffOrder(OptOrdDirection, DerivativePositionType.Call, false).OrderTick.Di.StrikePriceDouble;
                else
                    instr1.StrikePrice = StockUtils.GetNearestOptionStrikePrice(di, true, isBuy);

                // in case of replay get price from the file 
                BrokerErrorCode code = BrokerErrorCode.Success;
                var strkPrc = (int)instr1.StrikePrice;
                if (AlgoParams.IsReplayMode)
                {
                    S.TickCall = StockUtils.GetNearestOptionTick(S.Ticks.CurrTick, S.TicksCall, ref strkPrc, S.TickCall, !isSqrOff, isBuy);
                    instr1.StrikePrice = strkPrc;
                }
                else
                {
                    var tick1 = S.TickCall = new DTick();
                    code = AlgoParams.Broker.GetDerivativeQuote(instr1.Symbol, instr1.InstrumentType, instr1.ExpiryDate, instr1.StrikePrice, out tick1.Di);
                }

                orderParams1.OrderToPlace = new StockOrder(OptOrdDirection, 0, S.TickCall);
                orderParams1.Di = S.TickCall.Di;
                if (code == BrokerErrorCode.Success)
                    new Thread(new ParameterizedThreadStart(PlaceOrder)).Start(orderParams1);
            }

            // put
            if (posCode == PositionAllowAlgoCode.ALLOWNEWPOS ||
                (posCode == PositionAllowAlgoCode.ALLOWSQUAREOFF && IsOpenPosition(DerivativePositionType.Put)))
            if ((S.OrdersExecuted & DerivativePositionType.Put) != DerivativePositionType.Put)
            {
                S.OrderParamsPut = new PlaceOrderParams(di, doNoPriceCheck, isMarketOrder, 10, DerivativePositionType.Put, sem);
                var orderParams2 = S.OrderParamsPut;
                var instr2 = orderParams2.Instr = AlgoParams.I.Clone();
                instr2.InstrumentType = instr2.InstrumentType == InstrumentType.FutureIndex ? InstrumentType.OptionPutIndex : InstrumentType.OptionPutStock;
                if (posCode == PositionAllowAlgoCode.ALLOWSQUAREOFF)
                    instr2.StrikePrice = GetPairedSquareOffOrder(OptOrdDirection, DerivativePositionType.Put, false).OrderTick.Di.StrikePriceDouble;
                else
                    instr2.StrikePrice = StockUtils.GetNearestOptionStrikePrice(di, false, isBuy);

                // in case of replay get price from the file 
                BrokerErrorCode code = BrokerErrorCode.Success;
                var strkPrc = (int)instr2.StrikePrice;
                if (AlgoParams.IsReplayMode)
                {
                    S.TickPut = StockUtils.GetNearestOptionTick(S.Ticks.CurrTick, S.TicksPut, ref strkPrc, S.TickPut, !isSqrOff, isBuy);
                    instr2.StrikePrice = strkPrc;
                }
                else
                {
                    var tick2 = S.TickPut = new DTick();
                    code = AlgoParams.Broker.GetDerivativeQuote(instr2.Symbol, instr2.InstrumentType, instr2.ExpiryDate, instr2.StrikePrice, out tick2.Di);
                }

                orderParams2.OrderToPlace = new StockOrder(OptOrdDirection, 0, S.TickPut);
                orderParams2.Di = S.TickPut.Di;

                if (code == BrokerErrorCode.Success)
                    new Thread(new ParameterizedThreadStart(PlaceOrder)).Start(orderParams2);
            }

            for (int i = 0; i < ordersPendingExecution; i++)
                sem.WaitOne();


            if (S.OrderParamsFut.IsOrderExecuted)
                S.OrdersExecuted |= DerivativePositionType.Future;
            if (posCode == PositionAllowAlgoCode.ALLOWNEWPOS)
            {
                if (S.OrderParamsCall.IsOrderExecuted || S.AttemptsOrderCall++ >= 15)
                    S.OrdersExecuted |= DerivativePositionType.Call;
                if (S.OrderParamsPut.IsOrderExecuted || S.AttemptsOrderPut++ >= 15)
                    S.OrdersExecuted |= DerivativePositionType.Put;
            }
            else if (posCode == PositionAllowAlgoCode.ALLOWSQUAREOFF)
            {
                if (IsOpenPosition(DerivativePositionType.Call))
                {
                    if (S.OrderParamsCall.IsOrderExecuted)
                        S.OrdersExecuted |= DerivativePositionType.Call;
                }
                else
                    S.OrdersExecuted |= DerivativePositionType.Call;

                if (IsOpenPosition(DerivativePositionType.Put))
                {
                    if (S.OrderParamsPut.IsOrderExecuted)
                        S.OrdersExecuted |= DerivativePositionType.Put;
                }
                else
                    S.OrdersExecuted |= DerivativePositionType.Put;
            }

            TraceTradeInfo(S.OrderParamsFut);
            if (posCode == PositionAllowAlgoCode.ALLOWNEWPOS ||
                (posCode == PositionAllowAlgoCode.ALLOWSQUAREOFF && IsOpenPosition(DerivativePositionType.Call)))
            TraceTradeInfo(S.OrderParamsCall);
            if (posCode == PositionAllowAlgoCode.ALLOWNEWPOS ||
                (posCode == PositionAllowAlgoCode.ALLOWSQUAREOFF && IsOpenPosition(DerivativePositionType.Put)))
            TraceTradeInfo(S.OrderParamsPut);

            if (S.OrdersExecuted != DerivativePositionType.All)
                return OrderPlaceAlgoCode.RETRY;

            var fut = S.OrderParamsFut;
            var call = S.OrderParamsCall;
            var put = S.OrderParamsPut;

            if (fut.OrderToPlace.OrderPosition == Position.BUY)
            {
                S.TotalBuyTrades++;
                _eodTradeStats.num_trades++;
            }
            else if (fut.OrderToPlace.OrderPosition == Position.SELL)
            {
                S.TotalSellTrades++;
            }
            TotalTurnover += fut.OrderToPlace.OrderPrice * fut.Instr.Qty;

            // Expected price calculations
            try
            {
                fut.OrderToPlace.ExpectedOrderPrice = fut.OrderToPlace.OrderPrice;

                if (!S.IsMarketClosing && !(S.IsNextDay && !AlgoParams.IsMarketClosingSquareOff))
                {
                    double expectedPriceDiffPerc = 0;
                    if (fut.OrderToPlace.OrderPosition == Position.BUY)
                        expectedPriceDiffPerc = Math.Abs(S.Ticks.PercChangeFromMin) - S.PercChangeThreshold;
                    else
                        expectedPriceDiffPerc = Math.Abs(S.Ticks.PercChangeFromMax) - S.PercChangeThreshold;
                    // Ideal conditions Order price setting. Not taking Bid/Ask price exactly (in data with us they are unavailable)
                    // just using LTP
                    // On next day, market opens afresh (may be highly different from previous  close (previous tick)) 
                    // so order price will be same as LTP
                    // but only when continuous algo (algo id 11) functions, because for SquareOff on marketClosing algo,
                    // anyway the squareoff was done and 1st trade will neevr be on 1st tick of nextDay
                    // in algo9 SO on 1st tick of next day is done only if previous day data was incomplete or something

                    fut.OrderToPlace.ExpectedOrderPrice += (fut.OrderToPlace.ExpectedOrderPrice * (expectedPriceDiffPerc / 100));
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
                var orders = new List<StockOrder>(3);
                orders.Add(fut.OrderToPlace);
                if (S.OrderParamsCall.IsOrderExecuted)
                    orders.Add(call.OrderToPlace);
                if (S.OrderParamsPut.IsOrderExecuted)
                    orders.Add(put.OrderToPlace);
                var squaredOffOrders = GetPairedSquareOffOrders(fut.OrderToPlace.OrderPosition, 
                    call.OrderToPlace.OrderPosition, true);
                double nettProfitAmt = DoUpdatesAfterSquareOff(orders, squaredOffOrders);

                string squareOffTradeDesc = string.Format("SquareOff: NettProfitAmt= " + (int)nettProfitAmt);
                FileTracing.TraceImpAlgoInformation(squareOffTradeDesc);
                if (!AlgoParams.IsMock)
                    MessagingUtils.SendAlertMessage("SquareOff Confirmed", squareOffTradeDesc);
            }
            // It is new order
            else
            {
                _minPriceBeforeSqrOff = _maxPriceBeforeSqrOff = fut.OrderToPlace.OrderPrice;
                S.OpenPositions.Add(fut.OrderToPlace);
                if (S.OrderParamsCall.IsOrderExecuted)
                    S.OpenPositionsCall.Add(call.OrderToPlace);
                if (S.OrderParamsPut.IsOrderExecuted)
                    S.OpenPositionsPut.Add(put.OrderToPlace);
            }

            S.OrdersExecuted = DerivativePositionType.None;
            S.AttemptsOrderCall = 0;
            S.AttemptsOrderPut = 0;
            if (posCode == PositionAllowAlgoCode.ALLOWNEWPOS)
                S.OrderTypesPendingCount = GetPairedSquareOffOrders(
                    orderDirection == Position.BUY ? Position.SELL : Position.BUY,
                    OptOrdDirection == Position.BUY ? Position.SELL : Position.BUY, false).Count;
            else if (posCode == PositionAllowAlgoCode.ALLOWSQUAREOFF) // next will be new position
                S.OrderTypesPendingCount = 3;

            // Add to Global orders list
            _allOrders.Add(fut.OrderToPlace);
            if (S.OrderParamsCall.IsOrderExecuted)
                _allOrders.Add(call.OrderToPlace);
            if (S.OrderParamsPut.IsOrderExecuted)
                _allOrders.Add(put.OrderToPlace);


            return OrderPlaceAlgoCode.SUCCESS;
        }
        protected void TraceTradeInfo(PlaceOrderParams p)
        {
            if (p.IsOrderExecuted && !p.IsRepeatCheck)
            {
                S.OrdersExecuted |= DerivativePositionType.Future;
                var tradeConfirmation = string.Format("Trade Confirmed: {0} {1} {4} qty at {2}. OrderRef= {3}", p.Instr.ToString(),
                    p.OrderToPlace.OrderPosition, p.OrderToPlace.OrderPrice, p.OrderToPlace.OrderRef, p.Instr.Qty);
                FileTracing.TraceImpAlgoInformation(tradeConfirmation);
                if (!AlgoParams.IsMock)
                    MessagingUtils.SendAlertMessage("Trade Confirmed", tradeConfirmation);
                p.IsRepeatCheck = true;
            }
        }
        protected double DoUpdatesAfterSquareOff(List<StockOrder> newOrders, List<StockOrder> squaredOffOrders)
        {
            var futNew = newOrders[0];
            var callNew = newOrders.Count > 1 ? newOrders[1] : null;
            var putNew = newOrders.Count > 2 ? newOrders[2] : null;

            var futSqOff = squaredOffOrders[0];
            var callSqOff = squaredOffOrders.Count > 1 ? squaredOffOrders[1] : null;
            var putSqOff = squaredOffOrders.Count > 2 ? squaredOffOrders[2] : null;

            //if (futNew.OrderPosition == Position.NONE || callNew.OrderPosition == Position.NONE || putNew.OrderPosition == Position.NONE)
            //    throw new Exception("Unexpected Invalid Position NONE for new order");

            if ((callSqOff != null && !StockUtils.IsCallOption(callSqOff.OrderTick.Di.InstrumentType)) || 
                (putSqOff != null && StockUtils.IsCallOption(putSqOff.OrderTick.Di.InstrumentType)))
                throw new Exception("Unexpected Invalid Position mismatch");

            if ((callSqOff != null && callNew == null) ||
                (callSqOff == null && callNew != null))
                throw new Exception("Unexpected Invalid Call Order mismatch");

            if ((putSqOff != null && putNew == null) ||
                (putSqOff == null && putNew != null))
                throw new Exception("Unexpected Invalid Put Order mismatch");

            double profitAmount0 = (futNew.OrderPrice - futSqOff.OrderPrice) * S.OrderParamsFut.Instr.Qty;
            if (futNew.OrderPosition == Position.BUY)
                profitAmount0 = -profitAmount0;
            double profitAmount1 = callSqOff == null ? 0 : (callSqOff.OrderPrice - callNew.OrderPrice) * S.OrderParamsCall.Instr.Qty;
            double profitAmount2 = putSqOff == null ? 0 : (putSqOff.OrderPrice - putNew.OrderPrice) * S.OrderParamsPut.Instr.Qty;
            double profitAmount = profitAmount0 + profitAmount1 + profitAmount2;

            double brokerageAmt0 = (AlgoParams.SquareOffBrokerageFactor * AlgoParams.PercBrokerage * S.OrderParamsFut.Instr.Qty * futNew.OrderPrice / 100);
            double brokerageAmt1 = callSqOff == null ? 0 : AlgoParams.OptionsIntradayBrokerage;
            double brokerageAmt2 = putSqOff == null ? 0 : AlgoParams.OptionsIntradayBrokerage;
            var brokerageAmt = brokerageAmt0 + brokerageAmt1 + brokerageAmt2;

            // Ideal conditions expected nettProfit
            // Expected loss should be threshold percent at max in ideal conditions.
            double expectedProfitAmount0 = (futNew.ExpectedOrderPrice - futSqOff.ExpectedOrderPrice) * S.OrderParamsFut.Instr.Qty;
            if (futNew.OrderPosition == Position.BUY)
                expectedProfitAmount0 = -expectedProfitAmount0;
            double expectedProfitAmount1 = callSqOff == null ? 0 : (callSqOff.ExpectedOrderPrice - callNew.ExpectedOrderPrice) * S.OrderParamsCall.Instr.Qty;
            double expectedProfitAmount2 = putSqOff == null ? 0 : (putSqOff.ExpectedOrderPrice - putNew.ExpectedOrderPrice) * S.OrderParamsPut.Instr.Qty;
            double expectedProfitAmount = expectedProfitAmount0 + expectedProfitAmount1 + expectedProfitAmount2;
            double expectedNettProfit = expectedProfitAmount - brokerageAmt; 

            if (profitAmount >= 0)
                _profitableOrders.Add(profitAmount);

            double nettProfitAmt = profitAmount - brokerageAmt;

            // Set Last trade profit perc
            S.LastSquareOffPercProfit = (nettProfitAmt * 100) / (futNew.OrderPrice * S.OrderParamsFut.Instr.Qty);

            if (nettProfitAmt >= 0)
                _nettProfitableOrders.Add(nettProfitAmt);
            else
                _lossOrders.Add(nettProfitAmt);

            futNew.OrderProfit = nettProfitAmt;

            _lastSqrOffTradePrice = futNew.OrderPrice;
            _lastSqrOffTradeProfit = nettProfitAmt;
            _lastSqrOffTradeTime = S.Ticks.CurrTick.Di.QuoteTime;


            S.TotalActualNettProfitAmt += nettProfitAmt;
            S.TotalBrokerageAmt += brokerageAmt;

            _eodTradeStats.actual_profit += nettProfitAmt;
            _eodTradeStats.brokerage += brokerageAmt;

            

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

        // ------- Order related methods -------- //
        protected void PlaceOrder(object obj)
        {
            PlaceOrderParams p = (PlaceOrderParams)obj;
            p.IsOrderExecuted = PlaceOrder(p.OrderToPlace, p.Di, p.Instr, p.MaxPriceDiffPerc, p.DoNoPriceCheck, p.IsMarketOrder);
            if (p.IsOrderExecuted)
                Interlocked.Decrement(ref S.OrderTypesPendingCount);
            var k = p.Sem.Release();
        }
        protected bool PlaceOrder(StockOrder orderToPlace, DerivativeSymbolQuote di, Instrument instr, double maxPriceDiffPerc, bool doNoPriceCheck = true, bool isMarketOrder = false)
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
                FileTracing.TraceOut(string.Format("{0} Invalid Position = {1}", instr.Description(), position));
                return false;
            }

            string str = di.UpdateTime.ToLongDateString() + " " + di.UpdateTime.ToLongTimeString() + ". Placing order \"" + instr.Description() + "\". Position=" + position.ToString() + ". OrderPrice=" + orderPrice + ". Quantity=" + instr.Qty;
            FileTracing.TraceTradeInformation(str);
            //Beep(100, 50);

            if (!isMarketOrder && !doNoPriceCheck)
            {
                // Check orderprice and ltp difference
                double diffPerc = (diff * 100) / di.LastTradedPriceDouble;
                if (diff < 0 && Math.Abs(diffPerc) > maxPriceDiffPerc)
                {
                    FileTracing.TraceOut(string.Format("{0} . Not ordering at {4}. LTP = {3}. Spread diff = {1}, diff % = {2}",
                        instr.Description(), diff, diffPerc, di.LastTradedPriceDouble, orderPrice));
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

                    orderPriceAdjusted = Math.Round(orderPrice + priceMargin, 2); // to get execution done ahead of anyone else for that bid/ask
                    errorCode = AlgoParams.Broker.PlaceDerivativeOrder(instr.Symbol,
                       instr.Qty,
                       isMarketOrder ? 0 : orderPriceAdjusted,
                       isMarketOrder ? OrderPriceType.MARKET : OrderPriceType.LIMIT,
                       orderDirection,
                       instr.InstrumentType,
                       instr.StrikePrice,
                       instr.ExpiryDate,
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


                var errMsg = string.Format("ERROR {4} in PlaceOrder.{1} {2} qty of {0} at {3}", instr.ToString(),
            orderDirection, instr.Qty, orderPriceAdjusted, errorCode);
                FileTracing.TraceOut(errMsg);
                MessagingUtils.SendAlertMessage("Order Failed", errMsg);
                return false;
                // BUGBUGC: handle the exception, Insufficient funds etc. all
                //throw new Exception("BSA:PlaceOrder:ErrorCode=" + errorCode);
            }

            // --- Check order status and execution price

            if (!(AlgoParams.IsMock || AlgoParams.IsReplayMode))
                hasOrderExecuted = LoopUntilFutureOrderIsExecutedOrCancelled(orderToPlace.OrderRef, instr, out executionPrice);
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
        protected bool LoopUntilFutureOrderIsExecutedOrCancelled(string orderReference, Instrument instr, out double executionPrice)
        {
            executionPrice = -1;

            while (true)
            {
                DateTime fromDate = DateTime.Now;
                DateTime toDate = DateTime.Now;
                OrderStatus orderStatus;
                BrokerErrorCode errorCode = BrokerErrorCode.Success;

                Thread.Sleep(2 * 1000);
                // Incase of mock or testing replay
                if (AlgoParams.IsMock || AlgoParams.IsReplayMode)
                {
                    orderStatus = OrderStatus.EXECUTED;
                }
                else
                {
                    errorCode = AlgoParams.Broker.GetOrderStatus(orderReference,
                                            instr.InstrumentType, fromDate, toDate, out orderStatus);
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
                            executionPrice = AlgoParams.Broker.GetTradeExecutionPrice(fromDate, toDate, instr.InstrumentType, orderReference);
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

        // ------- Remote Command methods -------- //
        protected override void UpdatePositionsOnReset()
        {
            if (S.TotalBuyTrades != S.TotalSellTrades)
            {
                int numPositions = S.TotalBuyTrades - S.TotalSellTrades;
                bool isBuy = numPositions < 0;
                var pos = isBuy ? Position.BUY : Position.SELL;
                var orders = new List<StockOrder>(3);
                var futOrder = new StockOrder(pos, S.Ticks.CurrTick.Di.LastTradedPriceDouble, S.Ticks.CurrTick);
                orders.Add(futOrder);
                orders.Add(S.OrderParamsCall.OrderToPlace); // for now let it be stale. otherwise should ideally get fresh tick here
                orders.Add(S.OrderParamsPut.OrderToPlace); // for now let it be stale. otherwise should ideally get fresh tick here
                var squaredOffOrders = GetPairedSquareOffOrders(S.OrderParamsFut.OrderToPlace.OrderPosition, 
                    S.OrderParamsCall.OrderToPlace.OrderPosition, true);
                if (squaredOffOrders.Count != 3) throw new Exception("No matching squareoff orders found");
                double nettProfitAmt = DoUpdatesAfterSquareOff(orders, squaredOffOrders);
                _lastPeak = S.Ticks.CurrTick;
            }
        }

        // ------- Tracing tradeSummary  methods -------- //
        protected List<StockOrder> GetCurrentOpenPositions(InstrumentType type = InstrumentType.FutureIndex)
        {
            List<StockOrder> list = new List<StockOrder>();
            if (StockUtils.IsInstrumentOptionType(type))
            {
                if (StockUtils.IsCallOption(type))
                    foreach (StockOrder o in S.OpenPositionsCall)
                        list.Add(new StockOrder(o));
                else
                    foreach (StockOrder o in S.OpenPositionsPut)
                        list.Add(new StockOrder(o));
            }
            else
                foreach (StockOrder o in S.OpenPositions)
                    list.Add(new StockOrder(o));
            return list;
        }
    }
}
