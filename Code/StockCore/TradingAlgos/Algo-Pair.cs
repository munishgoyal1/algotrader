using System;
using StockTrader.Core;
using StockTrader.Platform.Logging;
using StockTrader.Utilities;
using StockTrader.Common;

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
    public class AlgoPair : TradingAlgo
    {

        public AlgoPair(AlgoParams Params)
            : base(Params)
        {

        }

        public override void AddTick(SymbolTick si)
        {
            if (!AlgoUpdateTick(si))
            {
                PostProcAddTick();
                return;
            }

            //UpdateMinMax();

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

            if (AlgoParams.AlgoId < 30)
                Scalper10();
            else if (AlgoParams.AlgoId < 40)
                Scalper30();
            else if (AlgoParams.AlgoId < 50)
                Scalper40();
            else if (AlgoParams.AlgoId < 100)
                Scalper50();
            else if (AlgoParams.AlgoId < 120)
                Scalper100();
            else if (AlgoParams.AlgoId < 140)
                Scalper10();
            else 
                Scalper40();

            PostProcAddTick();
        }

        protected void Scalper10()
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
        }

        protected void Scalper30()
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
                    if (!isBuy && S.Ticks.PercChangeFromMax > 0)
                    {
                        S.IsMinProfitDirectionPercRaised = true;
                        S.PercChangeThreshold = (profitPerc - AlgoParams.PercMinProfit);
                    }
                    else if (isBuy && S.Ticks.PercChangeFromMin < 0)
                    {
                        S.IsMinProfitDirectionPercRaised = true;
                        S.PercChangeThreshold = (profitPerc - AlgoParams.PercMinProfit);
                    }
                }
                else if (S.IsOnceCrossedMinProfit && profitPerc > 0)
                {
                    if (!S.IsMinProfitDirectionPercRaised)
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
        }

        protected void Scalper40()
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
                    if (!isBuy && S.Ticks.PercChangeFromMax > 0)
                    {
                        S.IsMinProfitDirectionPercRaised = true;
                        S.PercChangeThreshold = (profitPerc - AlgoParams.PercMinProfit) / 2;
                    }
                    else if (isBuy && S.Ticks.PercChangeFromMin < 0)
                    {
                        S.IsMinProfitDirectionPercRaised = true;
                        S.PercChangeThreshold = (profitPerc - AlgoParams.PercMinProfit) / 2;
                    }
                }
                else if (S.IsOnceCrossedMinProfit && profitPerc > 0)
                {
                    if (!S.IsMinProfitDirectionPercRaised)
                        S.PercChangeThreshold = profitPerc / 2;
                }
                else S.PercChangeThreshold = AlgoParams.PercSquareOffThreshold;
            }
            else S.PercChangeThreshold = AlgoParams.PercMarketDirectionChange;

            // detect peaks for fresh order and squareoff trigger points
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
        }

        protected void Scalper50()
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
                    S.IsOnceCrossedMinProfit = true;
                    if (!isBuy && S.Ticks.PercChangeFromMax > 0)
                    {
                        S.IsMinProfitDirectionPercRaised = true;
                        S.PercChangeThreshold = (profitPerc - AlgoParams.PercMinProfit) / 2;

                        if (AlgoParams.AlgoId >= 70 && AlgoParams.AlgoId < 90)
                            S.PercChangeThreshold = Math.Max(profitPerc / 3, S.PercChangeThreshold);
                    }
                    else if (isBuy && S.Ticks.PercChangeFromMin < 0)
                    {
                        S.IsMinProfitDirectionPercRaised = true;
                        S.PercChangeThreshold = (profitPerc - AlgoParams.PercMinProfit) / 2;

                        if (AlgoParams.AlgoId >= 70 && AlgoParams.AlgoId < 90)
                            S.PercChangeThreshold = Math.Max(profitPerc / 3, S.PercChangeThreshold);
                    }
                }
                else if (S.IsOnceCrossedMinProfit && profitPerc > 0)
                {
                    if (!S.IsMinProfitDirectionPercRaised && AlgoParams.AlgoId < 80)
                        //profitPerc += 0.0001;// 
                        S.PercChangeThreshold = profitPerc / 2;
                }
                else S.PercChangeThreshold = AlgoParams.PercSquareOffThreshold;
            }
            else S.PercChangeThreshold = AlgoParams.PercMarketDirectionChange;

            // detect peaks for fresh order and squareoff trigger points
            bool isOrderExecuted = FindPeaksAndOrder();

            // reset values on order executed
            if (isOrderExecuted)
            {
                S.IsOnceCrossedMinProfit = false;
                S.IsMinProfitDirectionPercRaised = false;
                if (numOpenPositions != 0) // this was square off trade, then reset the peak
                {
                    //if (S.LastSquareOffPercProfit > 1.5 * AlgoParams.PercMinProfit)
                    //{

                    //}
                    S.LastPeakType = PeakType.NONE;

                    if (AlgoParams.AlgoId >= 60)
                        S.Ticks.ResetMinMax();
                }
            }
        }

        protected void Scalper100()
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
                    S.IsOnceCrossedMinProfit = true;
                    if ((!isBuy && S.Ticks.PercChangeFromMax > 0) || (isBuy && S.Ticks.PercChangeFromMin < 0))
                    {
                        S.IsMinProfitDirectionPercRaised = true;
                        S.PercChangeThreshold = Math.Max(profitPerc / 3, (profitPerc - AlgoParams.PercMinProfit) / 2); //profitPerc/3 range 1/3 to 1/2 is good
                    }
                }
                else if (S.IsOnceCrossedMinProfit && profitPerc > 0)
                {
                    if (!S.IsMinProfitDirectionPercRaised)
                        S.PercChangeThreshold = profitPerc / 2.5;  // Range 2-3 is good
                }
                else S.PercChangeThreshold = AlgoParams.PercSquareOffThreshold;
            }
            else S.PercChangeThreshold = AlgoParams.PercMarketDirectionChange;

            // detect peaks for fresh order and squareoff trigger points
            bool isOrderExecuted = FindPeaksAndOrder();

            // reset values on order executed
            if (isOrderExecuted)
            {
                S.IsOnceCrossedMinProfit = false;
                S.IsMinProfitDirectionPercRaised = false;

                // This is must. Otherwise no algo profitable
                if (numOpenPositions != 0) // this was square off trade, then reset the peak
                {
                    //if (S.LastSquareOffPercProfit > 1.5 * AlgoParams.PercMinProfit)
                    //{

                    //}
                    S.LastPeakType = PeakType.NONE; // Must do

                    //Doesnt work good. Better not to rest it.
                    //S.Ticks.MinTick = S.Ticks.MaxTick = S.Ticks.CurrTick;
                }
            }
        }

        protected void Scalper100Locked()
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
                    S.IsOnceCrossedMinProfit = true;
                    if ((!isBuy && S.Ticks.PercChangeFromMax > 0) || (isBuy && S.Ticks.PercChangeFromMin < 0))
                    {
                        S.IsMinProfitDirectionPercRaised = true;
                        S.PercChangeThreshold = Math.Max(profitPerc / 3, (profitPerc - AlgoParams.PercMinProfit) / 2); //profitPerc/3 range 1/3 to 1/2 is good
                    }
                }
                else if (S.IsOnceCrossedMinProfit && profitPerc > 0) // No visible difference even if this condition is altogether removed
                {
                    if (!S.IsMinProfitDirectionPercRaised)
                        S.PercChangeThreshold = profitPerc / 2.5;  // Range 2-3 is good
                }
                else S.PercChangeThreshold = AlgoParams.PercSquareOffThreshold;
            }
            else S.PercChangeThreshold = AlgoParams.PercMarketDirectionChange;

            // detect peaks for fresh order and squareoff trigger points
            bool isOrderExecuted = FindPeaksAndOrder();

            // reset values on order executed
            if (isOrderExecuted)
            {
                S.IsOnceCrossedMinProfit = false;
                S.IsMinProfitDirectionPercRaised = false;

                // This is must. Otherwise no algo profitable
                if (numOpenPositions != 0) // this was square off trade, then reset the peak
                {
                    //if (S.LastSquareOffPercProfit > 1.5 * AlgoParams.PercMinProfit)
                    //{

                    //}
                    S.LastPeakType = PeakType.NONE; // Must do

                    //Doesnt work good. Better not to rest it.
                    //S.Ticks.MinTick = S.Ticks.MaxTick = S.Ticks.CurrTick;
                }
            }
        }
    }
}
