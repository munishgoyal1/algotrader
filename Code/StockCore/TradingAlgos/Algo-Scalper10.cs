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
    public class AlgoScalper10 : TradingAlgo
    {

        public AlgoScalper10(AlgoParams Params)
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
                    AlgoTryPlaceOrder(isBuy, S.CurrTick, marketClosing);
                }
                else S.PercChangeThreshold = AlgoParams.PercSquareOffThreshold;
            }
            else S.PercChangeThreshold = AlgoParams.PercMarketDirectionChange;

            // detect peaks and square off trigger points
            bool isOrderPlaceSuccess = FindPeaksAndOrder(marketClosing);

            PostProcAddTick();
        }
    }
}
