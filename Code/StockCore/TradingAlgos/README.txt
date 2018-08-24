
1.      
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
						
2.		//Doesnt work good for non-nifty. Better not to rest it.
        if (AlgoParams.I.Symbol == "NIFTY")
        S.Ticks.ResetMinMax();

3. // This is must peaktype reset. Otherwise no algo profitable
                if (numOpenPositions != 0) // this was square off trade, then reset the peak
                {
                    //if (S.LastSquareOffPercProfit > 1.5 * AlgoParams.PercMinProfit)
                    //{

                    //}
                    S.LastPeakType = PeakType.NONE; // Must do

                    //Doesnt work good. Better not to rest it.
                    //S.Ticks.MinTick = S.Ticks.MaxTick = S.Ticks.CurrTick;
                }


-----------------------------------------------------------------------------------------------------------------------
Algo# Description
1     Simple Min-Max with Daily squareoff         
2     Algo#1 + Stop loss after certain daily limit or number of trades (if final till that point in loss)
3     Algo#1 , but direction param getting halved while detecting squareoff points
4     Simple 1 daily trade limit

5
6     1st leg as per normal, then only at market closing, whatever the price do squareoff. Only 1 trade per day.
7     Only 1 trade as per normal , then squareoff is as per squareoff trigger and min profit. If cant havemin profit, then usual squareoff as per direction or market clsoing squareoff



 bool isPeakDetected = false;
            bool isSquareOffPointDetected = false;
            bool isSquareOff = false;

            if (AlgoParams.IsSingleTradePerDay)
            {
                //# Algo 7
                //if (EodTradeStats.num_trades == 1 && Math.Abs(S.TotalBuyTrades - S.TotalSellTrades) == 0)
                //    return;

                //# Algo 6
                if (Math.Abs(S.TotalBuyTrades - S.TotalSellTrades) == 1)
                    return;

                // # Algo 5
                //if (EodTradeStats.num_trades == AlgoParams.NumTradesStopForDay && Math.Abs(S.TotalBuyTrades - S.TotalSellTrades) == 0)
                //    return;
                //if (Math.Abs(S.TotalBuyTrades - S.TotalSellTrades) != 0)
                //    mPercChangeThreshold = AlgoParams.PercSquareOffThreshold;
                //else if (EodTradeStats.num_trades == 1 && Math.Abs(S.TotalBuyTrades - S.TotalSellTrades) == 0)
                //    return;
                //else
                //    mPercChangeThreshold = AlgoParams.PercMarketDirectionChange;
                //if (Math.Abs(S.TotalBuyTrades - S.TotalSellTrades) != 0)
                //    mPercChangeThreshold = AlgoParams.PercSquareOffThreshold;
                //else
                //    mPercChangeThreshold = AlgoParams.PercMarketDirectionChange;
            }

            // 1. CHECK TOP

            // detect peaks and square off trigger points
            if (lastPeakType == MarketDirection.DOWN)
            {