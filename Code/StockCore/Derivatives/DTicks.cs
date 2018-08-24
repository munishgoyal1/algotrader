using System;
using System.Collections.Generic;
using StockTrader.API.TradingAlgos;
using StockTrader.Platform.Logging;
using StockTrader.Utilities;
using StockTrader.Utilities.Broker;

namespace StockTrader.Core.TradingAlgos
{
    [Serializable()]
    public class DTicks
    {
        public DTicks()
        {
            AllTicks = new SortedDictionary<DateTime, List<DTick>>(new TickTimeComparer());
        }

        [NonSerialized()]
        public SortedDictionary<DateTime, List<DTick>> AllTicks;

        public DTick MinTick; // Current Bottom found or in process of discovery. New bottoms will update it
        public DTick MaxTick; // Current Top found or in process of discovery. New tops will update it
        public DTick PrevTick;
        public DTick CurrTick;

        public double PercChangeFromMax;
        public double PercChangeFromMin;
        public MarketDirection DominantDirection = MarketDirection.NONE;

        public bool IsInited;

        public void Init()
        {
            if (IsInited)
                return;

            IsInited = true;

            MinTick = MaxTick = PrevTick = CurrTick;

            AddTick(CurrTick);
        }

        public void ReInit(bool isConsiderPrevClosing)
        {
            IsInited = false;
            AllTicks.Clear();

            if (isConsiderPrevClosing)
                CurrTick = PrevTick;
            Init();
        }

        public void AddTick(DTick tick)
        {
            try
            {
                if (!IsInited) { CurrTick = tick; return; }

                if (AllTicks == null) { AllTicks = new SortedDictionary<DateTime, List<DTick>>(new TickTimeComparer()); }

                //var sec = tick.Di.QuoteTime.Second;
                var time = GeneralUtils.RoundUp(tick.Di.QuoteTime, TimeSpan.FromMinutes(1));

                if (!AllTicks.ContainsKey(time))
                    AllTicks.Add(time, new List<DTick>(6));

                AllTicks[time].Add(tick);

                PrevTick = CurrTick;
                CurrTick = tick;

                UpdateMinMax();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        public DTick GetTickXMinutesOld(int minutes)
        {
            var time = GeneralUtils.RoundUp(DateTime.Now.AddMinutes(-minutes), TimeSpan.FromMinutes(1));

            while (MarketUtils.IsTimeAfter915(time) && !AllTicks.ContainsKey(time) || AllTicks[time].Count == 0)
            {
                time = time.AddMinutes(-1);
            }

            if (AllTicks.ContainsKey(time))
                return AllTicks[time][0];
            else return null;
        }

        public void UpdateMinMax()
        {
            // Update Min Max
            DerivativeSymbolQuote maxDi = MaxTick.Di;
            DerivativeSymbolQuote minDi = MinTick.Di;
            PercChangeFromMax = 100 * ((StockUtils.GetPriceForAnalysis(CurrTick.Di) - StockUtils.GetPriceForAnalysis(maxDi)) / StockUtils.GetPriceForAnalysis(maxDi));
            PercChangeFromMin = 100 * ((StockUtils.GetPriceForAnalysis(CurrTick.Di) - StockUtils.GetPriceForAnalysis(minDi)) / StockUtils.GetPriceForAnalysis(minDi));

            if (PercChangeFromMax >= 0)
            {
                // New Max
                MaxTick = CurrTick;
            }
            else if (PercChangeFromMin <= 0)
            {
                // New Min
                MinTick = CurrTick;
            }

            // Get trend
            if (Math.Abs(CurrTick.Di.PercChangeFromPrevious) > 1.5)
            {
                DominantDirection = CurrTick.Di.PercChangeFromPrevious > 0 ? MarketDirection.UP : MarketDirection.DOWN;
            }
        }

        public void ResetMinMax()
        {
            MinTick = MaxTick = CurrTick;
        }

        public void ResetMinMax(bool isBuy)
        {
            if (!isBuy)
            {
                MinTick = CurrTick;
            }
            else
            {
                MaxTick = CurrTick;
            }
        }

        public void ResetMinMaxAndGetLastPeak(bool isBuy, out DTick lastPeak)
        {
            if (!isBuy)
                lastPeak = MaxTick;
            else
                lastPeak = MinTick;

            ResetMinMax(isBuy);
        }
    }

    [Serializable()]
    public class TickTimeComparer : IComparer<DateTime>
    {
        public int Compare(DateTime x, DateTime y)
        {
            var timeX = GeneralUtils.RoundUp(x, TimeSpan.FromMinutes(1));
            var timeY = GeneralUtils.RoundUp(y, TimeSpan.FromMinutes(1));
            if (timeX == timeY) return 0;
            else if (timeX > timeY) return 1;
            else return -1;
        }
    }
}
