using System;
using System.Collections.Generic;
using System.Text;
using StockTrader.Common;
using StockTrader.Core;
using StockTrader.Core.TradingAlgos;

namespace StockTrader.API.TradingAlgos
{
    public enum MarketDirection
    {
        NONE,
        UP,
        DOWN
    }

    public enum PeakType
    {
        NONE,
        BOTTOM,
        TOP
    }

    // Stats
    public class DayNett
    {
        public DateTime Day;
        public double Nett;

        public DayNett(DateTime day, double nett)
        {
            Day = day;
            Nett = nett;
        }
    }

    [Serializable()]
    public class DTick
    {
        public DTick()
        {
        }
        public DTick(DerivativeSymbolQuote di, int tickNumber)
        {
            Di = di;
            TickNumber = tickNumber;
        }
        public DerivativeSymbolQuote Di;
        public int TickNumber;
    }

    public class AlgoRunStats
    {
        public ProgramStats ProgStats = new ProgramStats();
        public ProfitLossStats PnLStats = new ProfitLossStats();
    }
    public class ProgramStats
    {
        public int FailedOrders;
    }

    public class ProfitLossStats
    {
        public double Brokerage;
        public double Profit;
        public int BuyTrades;
        public int SellTrades;
        public double Outstanding;
        public double NettAfterSquareOffAtClosingPrice;
    }

    public class AlgoData
    {
        public string TickFile;
        public AddTickChartDelegate DataDelegate;
        public Action<ChartTickPoint> ChartDelegate;
        public AlgoData(string tickFile, Action<ChartTickPoint> chartDelegate, AddTickChartDelegate dataDelegate)
        {
            TickFile = tickFile;
            ChartDelegate = chartDelegate;
            DataDelegate = dataDelegate;
        }
    }

    public class TickFileMetadata
    {
        public string Symbol;
        public string Path;
        public string R2;
        public string R1;
        public TickFormatType TickFormatType;
    }

    public class AlgoMetadata// : AlgoData
    {
        public DateTime StartTime;
        public DateTime EndTime;
        public List<TickFileMetadata> TickMetadata;
        public bool IsForceUpdate;
        public AddTickChartDelegate DataDelegate;
        public Action<ChartTickPoint> ChartDelegate;
        public Action<string> LogDelegate;
        //public string R1;
        //public string R2;
        public AlgoParams AlgoParams;

        public AlgoMetadata(List<TickFileMetadata> tickMetadata, //string r1, string r2,
            DateTime start, DateTime end, AlgoParams algoParams, bool isForceUpdate,
            Action<ChartTickPoint> chartDelegate, AddTickChartDelegate dataDelegate, Action<string> logWinDelegate)
        {
            TickMetadata = tickMetadata;
            //R1 = r1;
            //R2 = r2;
            StartTime = start;
            EndTime = end;
            ChartDelegate = chartDelegate;
            DataDelegate = dataDelegate;
            LogDelegate = logWinDelegate;
            IsForceUpdate = isForceUpdate;
            AlgoParams = algoParams;
        }
    }

    [Serializable()]
    public class AlgoState
    {
        public int TotalTickCount = 0;
        public bool IsFirstAlgoTickSeen = false;
        public double PercChangeThreshold;
        public bool IsOnceCrossedMinProfit = false;
        public DateTime LastOrderTime = DateTime.Now;
        public bool IsGoodPercMade = false;
        public bool IsMinProfitDirectionPercRaised = false;

        public DTicks Ticks = new DTicks();
        public DTick TickCall = new DTick();
        public DTick TickPut = new DTick();

        public Dictionary<int, Dictionary<int, SymbolTick>> TicksCall;
        public Dictionary<int, Dictionary<int, SymbolTick>> TicksPut;

        public bool IsOrderExecutionPending = false;  // Order execution couldnt complete, need retry
        public double LastSquareOffPercProfit = 0;
        
        public List<StockOrder> OpenPositions = new List<StockOrder>();
        public List<StockOrder> OpenPositionsCall = new List<StockOrder>();
        public List<StockOrder> OpenPositionsPut = new List<StockOrder>();

        public PlaceOrderParams OrderParamsFut;
        public PlaceOrderParams OrderParamsCall;
        public PlaceOrderParams OrderParamsPut;

        public int AttemptsOrderCall;
        public int AttemptsOrderPut;

        public DerivativePositionType OrdersExecuted = DerivativePositionType.None;
        public int OrderTypesPendingCount = 3;

        public PeakType LastPeakType = PeakType.NONE;
        
        
        public bool IsNextDay = false;
        public int TotalBuyTrades = 0;
        public int TotalSellTrades = 0;
        public double TotalBrokerageAmt = 0;
        public double TotalActualNettProfitAmt;
        public DateTime DayToday = DateTime.Now;
        public bool IsEODWindupDone = false;
        public bool IsMarketClosing = false;

        public bool IsSquareoffAtProfit;
        public bool IsPauseAfterSquareOffAtProfit;

        // Mapped fields from Algo
        public bool DoStopAlgo;
        public AlgoOrderPlaceState AlgoWorkingState = AlgoOrderPlaceState.RUNNING;
        public bool IsExternallySuspended;
    }

    public class AlgoParams
    {
        public Instrument I;
        public string R1;
        public string R2;
        public bool IsMock;
        public bool IsReplayMode;
        public bool IsHedgeAlgo;
        public string ReplayTickFile;
        public string PositionsFile;
        public string StateFile;
        public short AlgoId;
        public bool UseProbableTradeValue;
        public bool IsMarketClosingSquareOff;
        public bool IsSquareOffTrigger;
        public bool IsMinProfitMust;
        public double PercMarketDirectionChange;
        public double PercSquareOffThreshold;
        public double PercStoploss;
        public double PercMinProfit;
        public bool AllowShort;
        public bool AllowLong;
        public List<StockOrder> StartOrders;
        public List<StockOrder> StartOrdersCall;
        public List<StockOrder> StartOrdersPut;
        public IBroker Broker;
        public int MaxTotalPositions;
        public int MaxLongPositions;
        public int MaxShortPositions;
        public double LongCeilingPrice;
        public double ShortFloorPrice;
        public double PercPositionSpacing;
        public double OptionsIntradayBrokerage = 140;
        public double PercBrokerage;
        public double SquareOffBrokerageFactor;
        public double MarginFraction;

        public bool IsLimitLossPerDay;
        public double PercLossStopForDay;
        public double NumNettLossTradesStopForDay;
        public bool IsFixedTradesPerDay;
        public double NumTradesStopForDay;

        public bool IsConsiderPrevClosing;
        public bool AllowInitialTickStabilization;

        public int AlgoIntervalInSeconds;

        public override string ToString()
        {
            return string.Format("{0}_{1}_{2}", I.Description(), AlgoId, PercMarketDirectionChange);
        }

        public AlgoParams Clone()
        {
            var Clone = new AlgoParams();
            Clone.I = I == null ? null : I.Clone();
            Clone.R1 = R1;
            Clone.R2 = R2;
            Clone.IsMock = IsMock;
            Clone.IsReplayMode = IsReplayMode;
            Clone.IsHedgeAlgo = IsHedgeAlgo;
            Clone.ReplayTickFile = ReplayTickFile;
            Clone.PositionsFile = PositionsFile;
            Clone.StateFile = StateFile;
            Clone.AlgoId = AlgoId;
            Clone.UseProbableTradeValue = UseProbableTradeValue;
            Clone.IsMarketClosingSquareOff = IsMarketClosingSquareOff;
            Clone.IsSquareOffTrigger = IsSquareOffTrigger;
            Clone.IsMinProfitMust = IsMinProfitMust;
            Clone.PercMarketDirectionChange = PercMarketDirectionChange;
            Clone.PercSquareOffThreshold = PercSquareOffThreshold;
            Clone.PercStoploss = PercStoploss;
            Clone.PercMinProfit = PercMinProfit;
            Clone.AllowShort = AllowShort;
            Clone.AllowLong = AllowLong;
            Clone.StartOrders = StartOrders == null ? null : new List<StockOrder>(StartOrders);
            Clone.StartOrdersCall = StartOrdersCall == null ? null : new List<StockOrder>(StartOrdersCall);
            Clone.StartOrdersPut = StartOrdersPut == null ? null : new List<StockOrder>(StartOrdersPut);
            Clone.Broker = Broker;
            Clone.MaxTotalPositions = MaxTotalPositions;
            Clone.MaxLongPositions = MaxLongPositions;
            Clone.MaxShortPositions = MaxShortPositions;
            Clone.LongCeilingPrice = LongCeilingPrice;
            Clone.ShortFloorPrice = ShortFloorPrice;
            Clone.PercPositionSpacing = PercPositionSpacing;
            Clone.OptionsIntradayBrokerage = OptionsIntradayBrokerage;
            Clone.PercBrokerage = PercBrokerage;
            Clone.SquareOffBrokerageFactor = SquareOffBrokerageFactor;
            Clone.MarginFraction = MarginFraction;
            Clone.PercLossStopForDay = PercLossStopForDay;
            Clone.NumNettLossTradesStopForDay = NumNettLossTradesStopForDay;
            Clone.IsLimitLossPerDay = IsLimitLossPerDay;
            Clone.NumTradesStopForDay = NumTradesStopForDay;
            Clone.AlgoIntervalInSeconds = AlgoIntervalInSeconds;
            Clone.IsConsiderPrevClosing = IsConsiderPrevClosing;
            Clone.AllowInitialTickStabilization = AllowInitialTickStabilization;
            return Clone;
        }

        public string Description()
        {
            StringBuilder sb = new StringBuilder();
            //mgoyalTODO: sb.AppendFormat("I{0}-A{1}-MDP{2}-SO{3}-MP{4}", I.Description(), AlgoId, PercMarketDirectionChange, PercSquareOffThreshold, PercMinProfit);
            sb.AppendFormat("I{0}-A{1}", I.Description(), AlgoId);
            return sb.ToString();
        }
    }

    public class OrderSorter : IComparer<StockOrder>
    {
        #region IComparer<StockOrder> Members

        public int Compare(StockOrder x, StockOrder y)
        {
            //For 2 orders which are SELL, place the one with the lower TradePrice
            //first, so that we can square that off first.
            if (x.OrderPosition == Position.SELL && y.OrderPosition == Position.SELL)
            {
                return x.OrderPrice.CompareTo(y.OrderPrice);
            }
            //For 2 orders which are BUY, place the one with the higher TradePrice
            //first, so that we can square that off first.
            if (x.OrderPosition == Position.BUY && y.OrderPosition == Position.BUY)
            {
                return (-1) * x.OrderPrice.CompareTo(y.OrderPrice);
            }

            //we need to return a deterministic value.
            return x.OrderPrice.CompareTo(y.OrderPrice);
        }

        #endregion
    }
}
