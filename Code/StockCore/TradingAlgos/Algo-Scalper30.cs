using System;
using StockTrader.Core;

// MD = MoreDebug
// DB = DataBase
// MC = MarketClosing
// SO = SquareOff
// EOD = End of Day
// EOP = End of Period

// SOMC-DB = SquareOff on MarketClosing, Runs on IntraDay Tick data and maintains EOD trading stats (profit/loss, num trades etc.) incl. EOP stats

namespace StockTrader.API.TradingAlgos
{
    public class AlgoScalper30 : TradingAlgo
    {

        public AlgoScalper30(AlgoParams Params)
            : base(Params)
        {

        }

        public override void AddTick(SymbolTick si)
        {
            bool marketClosing = false;
            if (!AlgoUpdateTick(si, out marketClosing))
            {
                PostProcAddTick();
                return;
            }

            UpdateMinMax();

            // Try Profitable SquareOff
            int numOpenPositions = Math.Abs(S.TotalBuyTrades - S.TotalSellTrades);
            if (numOpenPositions != 0)
            {
                bool isBuy = S.TotalBuyTrades < S.TotalSellTrades;
                double profitPerc = GetSquareOffProfitPerc();

                if (profitPerc > AlgoParams.PercMinProfit)
                {
                    S.IsOnceCrossedMinProfit = true;
                    if (!isBuy && S.PercChangeFromMax > 0)
                    {
                        S.IsMinProfitDirectionPercRaised = true;
                        S.PercChangeThreshold = (profitPerc - AlgoParams.PercMinProfit);
                    }
                    else if (isBuy && S.PercChangeFromMin < 0)
                    {
                        S.IsMinProfitDirectionPercRaised = true;
                        S.PercChangeThreshold = (profitPerc - AlgoParams.PercMinProfit);
                    }
                }
                else if (S.IsOnceCrossedMinProfit && profitPerc > 0)
                {
                    if(!S.IsMinProfitDirectionPercRaised)
                        S.PercChangeThreshold = profitPerc / 2;
                }
                else S.PercChangeThreshold = AlgoParams.PercSquareOffThreshold;
            }
            else S.PercChangeThreshold = AlgoParams.PercMarketDirectionChange;

            // detect peaks and square off trigger points
            bool isOrderPlaceSuccess = FindPeaksAndOrder();

            // reset values on order placement
            if (isOrderPlaceSuccess)
            {
                S.IsOnceCrossedMinProfit = false;
                S.IsMinProfitDirectionPercRaised = false;
                numOpenPositions = Math.Abs(S.TotalBuyTrades - S.TotalSellTrades);
                if (numOpenPositions == 0) // this was square off trade, then reset the peak
                    S.LastPeakType = PeakType.NONE;
            }

            PostProcAddTick();
        }
    }
}
