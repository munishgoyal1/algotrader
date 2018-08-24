using System;
using StockTrader.Core;
using StockTrader.Platform.Logging;
using StockTrader.Utilities;
using StockTrader.Common;
using StockTrader.Utilities.Broker;

// MD = MoreDebug
// DB = DataBase
// MC = MarketClosing
// SO = SquareOff
// EOD = End of Day
// EOP = End of Period

// SOMC-DB = SquareOff on MarketClosing, Runs on IntraDay Tick data and maintains EOD trading stats (profit/loss, num trades etc.) incl. EOP stats
// Work on Algo-Pairs not started !!! This is just a placeholder copy from Algo-Scalper
namespace StockTrader.API.TradingAlgos
{
    public class AlgoHedge : TradingAlgoHedge
    {

        public AlgoHedge(AlgoParams Params)
            : base(Params)
        {

        }

        protected void AlgoStateMgmtAfterRemoteCommand(bool isSquareoff)
        {
            if (isSquareoff)
                if (AlgoParams.AlgoId >= 60 && AlgoParams.AlgoId < 120)
                    S.Ticks.ResetMinMax();
        }

        public override void AddTick(SymbolTick si)
        {
            if (!AlgoUpdateTick(si))
            {
                PostProcAddTick();
                return;
            }

            // Get call-put tick for mock, for live implement later
            if (AlgoParams.IsReplayMode)
            {
                bool isBuy = S.TotalBuyTrades < S.TotalSellTrades;
                if (S.TotalBuyTrades != S.TotalSellTrades)
                {
                    var ordCall = GetPairedSquareOffOrder(isBuy ? Position.BUY : Position.SELL, DerivativePositionType.Call, false);
                    if (ordCall != null)
                    {
                        var strkPrc = (int)ordCall.OrderTick.Di.StrikePriceDouble;
                        // get tick from tick store       
                        S.TickCall = StockUtils.GetNearestOptionTick(S.Ticks.CurrTick, S.TicksCall, ref strkPrc, S.TickCall);
                    }
                    else S.TickCall = null;

                    var ordPut = GetPairedSquareOffOrder(isBuy ? Position.BUY : Position.SELL, DerivativePositionType.Put, false);
                    if (ordPut != null)
                    {
                        var strkPrc = (int)ordPut.OrderTick.Di.StrikePriceDouble;
                        // get tick from tick store       
                        S.TickPut = StockUtils.GetNearestOptionTick(S.Ticks.CurrTick, S.TicksPut, ref strkPrc, S.TickPut);
                    }
                    else S.TickPut = null;
                }
            }

            // Remotely called squareoff
            if (S.IsSquareoffAtProfit)
            {
                if (S.TotalBuyTrades != S.TotalSellTrades)
                {
                    var profitPerc = GetSquareOffProfitPerc();
                    if (profitPerc > 0.05)
                    {
                        var orderPlaceCode = TryImmediateSquareOff(false, false, true);
                        if (orderPlaceCode == OrderPlaceAlgoCode.SUCCESS || orderPlaceCode != OrderPlaceAlgoCode.RETRY)
                        {
                            S.IsSquareoffAtProfit = false;
                            if (S.IsPauseAfterSquareOffAtProfit)
                            {
                                Pause(false);
                                S.IsPauseAfterSquareOffAtProfit = false;
                            }
                        }
                    }
                }
            }

            if (AlgoParams.AlgoId >= 150)
                Scalper20(1, 0.5);

            else if (AlgoParams.AlgoId < 20)
                Scalper10();
            else if (AlgoParams.AlgoId < 30)
                Scalper10(true, true);
            else if (AlgoParams.AlgoId < 40)
                Scalper20(1, 0.5);
            else if (AlgoParams.AlgoId < 50)
                Scalper20(0.5, 0.5);
            else if (AlgoParams.AlgoId < 100)
                Scalper20(0.5, 0.5, false, true);
            else if (AlgoParams.AlgoId > 115 && AlgoParams.AlgoId < 120)
                Scalper116();
            else 
                Scalper20(0.5, 0.4, true);

            PostProcAddTick();
        }

        protected void Scalper10(bool isResetPeakType = false, bool isResetMinMax = false)
        {
            // Try Profitable SquareOff
            int numOpenPositions = Math.Abs(S.TotalBuyTrades - S.TotalSellTrades);
            if (numOpenPositions != 0)
            {
                bool isBuy = S.TotalBuyTrades < S.TotalSellTrades;
                double profitPerc = GetSquareOffProfitPerc();
                _currProfit = profitPerc;

                if (profitPerc > AlgoParams.PercMinProfit)
                {
                    AlgoTryPlaceOrder(isBuy, S.Ticks.CurrTick);
                }
                else S.PercChangeThreshold = AlgoParams.PercSquareOffThreshold;
            }
            else S.PercChangeThreshold = AlgoParams.PercMarketDirectionChange;

            // detect peaks and square off trigger points
            bool isOrderPlaceSuccess = FindPeaksAndOrder();

            // reset values on order placement
            if (isOrderPlaceSuccess)
            {
                // numOpenPositions != 0 means it was square off trade
                if (isResetPeakType && numOpenPositions != 0) 
                    S.LastPeakType = PeakType.NONE;

                if (isResetMinMax && numOpenPositions != 0)
                    S.Ticks.ResetMinMax();
            }
        }
        protected void Scalper20(double percFactor1, double percFactor2, bool isPercLenient = false, bool isResetMinMax = false)
        {
            // Try Profitable SquareOff
            int numOpenPositions = Math.Abs(S.TotalBuyTrades - S.TotalSellTrades);
            if (numOpenPositions != 0)
            {
                bool isBuy = S.TotalBuyTrades < S.TotalSellTrades;
                double profitPerc = GetSquareOffProfitPerc();
                _currProfit = profitPerc;
                if (profitPerc > AlgoParams.PercMinProfit)
                {
                    S.IsOnceCrossedMinProfit = true;
                    if ((!isBuy && S.Ticks.PercChangeFromMax > 0) || (isBuy && S.Ticks.PercChangeFromMin < 0))
                    {
                        S.IsMinProfitDirectionPercRaised = true;
                        S.PercChangeThreshold = (profitPerc - AlgoParams.PercMinProfit) * percFactor1;

                        if (isPercLenient)
                            //    S.PercChangeThreshold = Math.Max(profitPerc / 3, S.PercChangeThreshold);
                            S.PercChangeThreshold = Math.Max(profitPerc / 3, (profitPerc - AlgoParams.PercMinProfit) / 2); //profitPerc/3 range 1/3 to 1/2 is good
                    }
                }
                else if (S.IsOnceCrossedMinProfit && profitPerc > 0)
                {
                    if (!S.IsMinProfitDirectionPercRaised)
                        S.PercChangeThreshold = profitPerc * percFactor2;
                }
                else S.PercChangeThreshold = AlgoParams.PercSquareOffThreshold;
            }
            else S.PercChangeThreshold = AlgoParams.PercMarketDirectionChange;

            // if 1st trade is lossy then UP the threshold
            //if ((_eodTradeStats.num_trades == _eodTradeStats.num_loss_trades || _eodTradeStats.num_loss_trades - _eodTradeStats.num_profit_trades >= 3)
            //    && _eodTradeStats.num_trades > 0 && numOpenPositions == 0)
            //{
            //    S.PercChangeThreshold *= 1.25;
            //}
            //else if (_lastSqrOffTradeProfit > 0 && _lastSqrOffTradeProfit * 100 / (_lastSqrOffTradePrice * 125) >= 1.5 * AlgoParams.PercMinProfit && numOpenPositions == 0)
            //{
            //    S.PercChangeThreshold *= 1.25;
            //}

            // detect peaks and square off trigger points
            bool isOrderPlaceSuccess = FindPeaksAndOrder();

            // reset values on order placement
            if (isOrderPlaceSuccess)
            {
                S.IsOnceCrossedMinProfit = false;
                S.IsMinProfitDirectionPercRaised = false;

                if (numOpenPositions != 0) // this was square off trade, then reset the peak
                    S.LastPeakType = PeakType.NONE;

                if (isResetMinMax && numOpenPositions != 0)
                    S.Ticks.ResetMinMax();
            }
        }


        // -------------- Work in Progress : TEST ------------------------//
        protected virtual bool FindPeaksAndOrder150()
        {
            int numOpenPositions = Math.Abs(S.TotalBuyTrades - S.TotalSellTrades);
            bool isFreshOrder = numOpenPositions == 0;
            //double percChange = 
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
        protected void Scalper1501()
        {
            // Try Profitable SquareOff
            double profitPerc = 0;
            int numOpenPositions = S.TotalBuyTrades - S.TotalSellTrades;
            if (numOpenPositions != 0)
            {
                bool isBuy = numOpenPositions < 0;
                profitPerc = GetSquareOffProfitPerc();
                _currProfit = profitPerc;

                if (profitPerc > AlgoParams.PercMinProfit)
                {
                    if (!S.IsOnceCrossedMinProfit)
                        S.PercChangeThreshold = Math.Min(profitPerc / 3, (profitPerc - AlgoParams.PercMinProfit));

                    S.IsOnceCrossedMinProfit = true;

                    if ((!isBuy && S.Ticks.PercChangeFromMax > 0) || (isBuy && S.Ticks.PercChangeFromMin < 0))
                    {
                        S.IsMinProfitDirectionPercRaised = true;
                        S.PercChangeThreshold = Math.Min(profitPerc / 3, (profitPerc - AlgoParams.PercMinProfit) / 2);

                        //if (AlgoParams.I.Symbol == "NIFTY")
                        //    S.PercChangeThreshold = Math.Max(profitPerc / 3, S.PercChangeThreshold);
                    }
                }
                else if (!S.IsOnceCrossedMinProfit && profitPerc > AlgoParams.PercMinProfit / 2)
                {
                    S.PercChangeThreshold = profitPerc / 2;  // Range 2-3 is good
                    //if (AlgoParams.I.Symbol == "NIFTY")
                    //    S.PercChangeThreshold = Math.Max(2 * profitPerc, S.PercChangeThreshold);  // Range 2-3 is good
                    S.IsGoodPercMade = true;
                }
                else if (!S.IsGoodPercMade && profitPerc > 0)
                {
                    S.PercChangeThreshold = Math.Max(profitPerc * 2, AlgoParams.PercSquareOffThreshold / 2);  // Range 2-3 is good
                }
                //else if (!S.IsOnceCrossedMinBy2Profit &&
                //    S.Ticks.CurrTick.Di.QuoteTime - S.LastOrderTime > new TimeSpan(3, 0, 0) &&
                //    (profitPerc < 0 && (Math.Abs(profitPerc) < AlgoParams.PercSquareOffThreshold * 0.5 && Math.Abs(profitPerc) > AlgoParams.PercSquareOffThreshold/4)))
                //{
                //    S.PercChangeThreshold = 0.05; // kind of immediate squareoff
                //}
                //else if (!S.IsOnceCrossedMinBy2Profit && profitPerc > 0 && S.Ticks.CurrTick.Di.QuoteTime - S.LastOrderTime > new TimeSpan(2, 0, 0))
                //{
                //    S.PercChangeThreshold = 0.05; // kind of immediate squareoff
                //}
                //else if (S.IsOnceCrossedMinBy2Profit && profitPerc > 0 && S.Ticks.CurrTick.Di.QuoteTime - S.LastOrderTime > new TimeSpan(3, 0, 0))
                //{
                //    S.PercChangeThreshold = 0.05;// profitPerc; // no profit no loss sort
                //}
                else S.PercChangeThreshold = AlgoParams.PercSquareOffThreshold;
            }
            else S.PercChangeThreshold = AlgoParams.PercMarketDirectionChange;

            // detect peaks for fresh order and squareoff trigger points
            bool isOrderExecuted = FindPeaksAndOrder();

            // reset values on order executed
            if (isOrderExecuted)
            {
                S.LastOrderTime = S.Ticks.CurrTick.Di.QuoteTime;
                S.IsOnceCrossedMinProfit = false;
                S.IsGoodPercMade = false;
                S.IsMinProfitDirectionPercRaised = false;

                // This is must. Otherwise no algo profitable
                if (numOpenPositions != 0) // this was square off trade, then reset the peak
                {
                    //if (S.LastSquareOffPercProfit > 1.5 * AlgoParams.PercMinProfit)
                    //{

                    //}
                    S.LastPeakType = PeakType.NONE; // Must do

                    //Doesnt work good. Better not to rest it.
                    //if (AlgoParams.I.Symbol == "NIFTY")
                    //S.Ticks.ResetMinMax();
                }
            }
        }
        protected void Scalper116()
        {
            // Try Profitable SquareOff
            double profitPerc = 0;
            bool isPlaceOrderTimeLapsed = false;
            bool isPlaceOrderMinProfit = false;
            bool isOrderExecuted = false;

            int numOpenPositions = S.TotalBuyTrades - S.TotalSellTrades;
            bool isBuy = numOpenPositions < 0;

            if (numOpenPositions != 0)
            {
                profitPerc = GetSquareOffProfitPerc();
                _currProfit = profitPerc;
                if (profitPerc > AlgoParams.PercMinProfit)
                {
                    S.IsOnceCrossedMinProfit = true;
                    //if ((!isBuy && S.Ticks.PercChangeFromMax > 0) || (isBuy && S.Ticks.PercChangeFromMin < 0))
                    //{
                    //    S.IsMinProfitDirectionPercRaised = true;
                    //    S.PercChangeThreshold = (profitPerc - AlgoParams.PercMinProfit) / 2;
                    //}

                    //isPlaceOrderMinProfit = true;
                    //isOrderExecuted = AlgoTryPlaceOrder(isBuy, S.Ticks.CurrTick) == OrderPlaceAlgoCode.SUCCESS;

                    S.PercChangeThreshold = profitPerc / 3;// (profitPerc - AlgoParams.PercMinProfit) / 2;
                }
                //else if (S.IsOnceCrossedMinProfit && profitPerc > 0)
                //{
                //    if (!S.IsMinProfitDirectionPercRaised)
                //        S.PercChangeThreshold = profitPerc / 3;
                //}
                //else if (S.IsOnceCrossedMinProfit && S.Ticks.CurrTick.Di.QuoteTime - S.LastOrderTime > new TimeSpan(0, 15, 0) && profitPerc > 0)
                //{
                //    isPlaceOrderTimeLapsed = true;
                //}
                else S.PercChangeThreshold = AlgoParams.PercSquareOffThreshold;
            }
            else S.PercChangeThreshold = AlgoParams.PercMarketDirectionChange;

            if (isPlaceOrderTimeLapsed)
            {
                var orderPlaceCode = TryImmediateSquareOff(false, false, true);

                if (orderPlaceCode == OrderPlaceAlgoCode.SUCCESS)
                {
                    S.Ticks.ResetMinMaxAndGetLastPeak(isBuy, out _lastPeak);

                    isOrderExecuted = true;
                }
            }
            else

                // detect peaks for fresh order and squareoff trigger points
                isOrderExecuted = FindPeaksAndOrder();

            // reset values on order executed
            if (isOrderExecuted)
            {
                //if (AlgoParams.IsReplayMode)
                S.LastOrderTime = S.Ticks.CurrTick.Di.QuoteTime;
                //else 
                // S.LastOrderTime = DateTime.Now;

                S.IsOnceCrossedMinProfit = false;
                S.IsMinProfitDirectionPercRaised = false;
                if (numOpenPositions != 0) // this was square off trade, then reset the peak
                {
                    //if (S.LastSquareOffPercProfit > 1.5 * AlgoParams.PercMinProfit)
                    //{

                    //}
                    S.LastPeakType = PeakType.NONE;
                    //if(AlgoParams.I.Symbol.ToUpper() == "NIFTY")
                    //    S.Ticks.MinTick = S.Ticks.MaxTick = S.Ticks.CurrTick;
                }
            }
        }
    }
}
