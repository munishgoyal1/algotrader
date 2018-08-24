using System;
using StockTrader.Core;
using StockTrader.Platform.Logging;

// MD = MoreDebug
// DB = DataBase
// MC = MarketClosing
// SO = SquareOff
// EOD = End of Day
// EOP = End of Period

// SOMC-DB = SquareOff on MarketClosing, Runs on IntraDay Tick data and maintains EOD trading stats (profit/loss, num trades etc.) incl. EOP stats

namespace StockTrader.API.TradingAlgos
{
    public class AlgoMinMax : TradingAlgo
    {

        public AlgoMinMax(AlgoParams Params)
            : base(Params)
        {

        }

        public override void AddTick(SymbolTick si)
        {
            bool marketClosing = false;
            if (!AlgoUpdateTick(si))
            {
                PostProcAddTick();
                return;
            }

            UpdateMinMax();

            OrderPlaceAlgoCode isOrderPlaceSuccess = OrderPlaceAlgoCode.ALGORISKREJECT;

            // ************** 1. CHECK TOP ********************** //
            // detect peaks and square off trigger points
            if (S.LastPeakType == PeakType.BOTTOM)
            {
                bool canOrderThisAnalysis = true; // whether can palce new order on same tick where squareoff is tried

                // Sell Squareoff check
                if (AlgoParams.IsSquareOffTrigger && S.PercChangeFromMax < 0 &&
                    Math.Abs(S.PercChangeFromMax) >= AlgoParams.PercSquareOffThreshold)
                {
                    int numOpenPositions = Math.Abs(S.TotalBuyTrades - S.TotalSellTrades);

                    if (numOpenPositions != 0)
                    {
                        // May be add quantity also to all these calculations
                        double profitPerc = GetSquareOffProfitPerc();

                        if (!AlgoParams.IsMinProfitMust || profitPerc > AlgoParams.PercMinProfit)
                        {
                            // SquareOff point reached
                            canOrderThisAnalysis = AlgoTryPlaceOrder(false, S.Ticks.CurrTick) != OrderPlaceAlgoCode.SUCCESS;
                        }
                    }
                }

                // New sell order
                if (S.PercChangeFromMax < 0 && Math.Abs(S.PercChangeFromMax) >= S.PercChangeThreshold)
                {
                    // Record tick abberations
                    if (Math.Abs(S.PercChangeFromMax) - S.PercChangeThreshold > 1)
                    {
                        Logger.LogWarningMessage(
                            string.Format("{3}: {0} : PriceTick non-continuous {1}: PercThreshold = {4} S.PercChangeFromMax = {2} times.",
                                          S.Ticks.CurrTick.Di.QuoteTime, _discontinuousMaxTickNum++,
                                          S.PercChangeFromMax / S.PercChangeThreshold, AlgoParams.I.Symbol, S.PercChangeThreshold));
                    }
                    // New top found
                    S.LastPeakType = PeakType.TOP;
                    _lastPeak = S.Ticks.MaxTick;
                    S.Ticks.MinTick = S.Ticks.CurrTick;
                    _allPeaks.Add(S.TotalTickCount, _lastPeak);

                    if (canOrderThisAnalysis)
                    {
                        isOrderPlaceSuccess = AlgoTryPlaceOrder(false, S.Ticks.CurrTick);
                    }
                }
            }
            // 2. CHECK BOTTOM
            else if (S.LastPeakType == PeakType.TOP)
            {
                bool canOrderThisAnalysis = true; // whether can palce new order on same tick where squareoff is tried

                // Look for squareoff
                if (AlgoParams.IsSquareOffTrigger && S.PercChangeFromMin > 0 &&
                    Math.Abs(S.PercChangeFromMin) >= AlgoParams.PercSquareOffThreshold)
                {
                    int numOpenPositions = Math.Abs(S.TotalBuyTrades - S.TotalSellTrades);

                    if (numOpenPositions != 0)
                    {
                        double profitPerc = GetSquareOffProfitPerc();

                        if (!AlgoParams.IsMinProfitMust || profitPerc > AlgoParams.PercMinProfit)
                        {
                            // SquareOff point reached
                            canOrderThisAnalysis = AlgoTryPlaceOrder(true, S.Ticks.CurrTick) != OrderPlaceAlgoCode.SUCCESS;
                        }
                    }
                }

                // New buy trade
                if (S.PercChangeFromMin > 0 && Math.Abs(S.PercChangeFromMin) >= S.PercChangeThreshold)
                {
                    // Record tick abberations
                    if (Math.Abs(S.PercChangeFromMin) - S.PercChangeThreshold > 1)
                    {
                        Logger.LogWarningMessage(
                            string.Format("{3}: {0} : PriceTick non-continuous {1}: PercThreshold = {4} S.PercChangeFromMin = {2} times.",
                                          S.Ticks.CurrTick.Di.QuoteTime, _discontinuousMaxTickNum++,
                                          S.PercChangeFromMax / S.PercChangeThreshold, AlgoParams.I.Symbol, S.PercChangeThreshold));
                    }

                    // New bottom found
                    S.LastPeakType = PeakType.BOTTOM;
                    _lastPeak = S.Ticks.MinTick;
                    S.Ticks.MaxTick = S.Ticks.CurrTick; // range of maxtick set at this point
                    _allPeaks.Add(S.TotalTickCount, _lastPeak);

                    if (canOrderThisAnalysis)
                    {
                        isOrderPlaceSuccess = AlgoTryPlaceOrder(true, S.Ticks.CurrTick);
                    }
                }
            }
            // 3. 1st PEAK (TOP/BOTTOM)
            else if (S.LastPeakType == PeakType.NONE)
            {
                // 1st peak
                double percDiffMinMax = 100 *
                                        ((GetPriceForAnalysis(S.Ticks.MaxTick.Di) - GetPriceForAnalysis(S.Ticks.MinTick.Di)) /
                                         GetPriceForAnalysis(S.Ticks.MinTick.Di)); // should be > than mPercChangeThreshold

                bool isBuy = S.PercChangeFromMin > 0 && Math.Abs(S.PercChangeFromMin) >= S.PercChangeThreshold;
                bool isSell = S.PercChangeFromMax < 0 && Math.Abs(S.PercChangeFromMax) >= S.PercChangeThreshold;

                if (isBuy && isSell)
                {
                    FileTracing.TraceOut("Error , 1st peak says both buy and sell. Bug in order placing, order not getting executed. Still recover from it..");
                    isBuy = Math.Abs(S.PercChangeFromMin) > Math.Abs(S.PercChangeFromMax);
                    isSell = !isBuy;
                }
                //if (Math.Abs(percDiffMinMax) >= mPercChangeThreshold)
                if (isBuy || isSell)
                {
                    isOrderPlaceSuccess = AlgoTryPlaceOrder(isBuy, S.Ticks.CurrTick);

                    if (isOrderPlaceSuccess == OrderPlaceAlgoCode.SUCCESS)
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
                        _allPeaks.Add(S.TotalTickCount, _lastPeak);
                    }
                }
            }

            PostProcAddTick();
        }
    }
}
