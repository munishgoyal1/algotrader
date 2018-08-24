using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using StockTrader.API.TradingAlgos;
using StockTrader.Common;
using StockTrader.Core;
using StockTrader.Platform.Logging;
using StockTrader.Utilities.Broker;
using System.Linq;

namespace StockTrader.Stocks.Utilities.Broker
{
    public partial class AlgoRunUtils
    {
        //short[] AlgoIds = new short[] { 1, 2, 11, 12, 15,  16, 17, 18, 19, 20, 21, 22, 23, 24, 25 };//, 13, 14 };
        //double[] MktDirectionParams = new[] {0.5, 1, 1.5, 2.5, 3, 3.5, 2, 0.25};

        //short[] AlgoIds = new short[] { 17, 18, 19, 30, 31, 32, 40, 41, 42, 50, 51, 52, 60, 61, 62, 72, 73, 82, 83, 92, 93 };
        //double[] MktDirectionParams = new[] { 0.4, 0.5, 0.6, 0.3, 0.2, 0.1};

        const bool DoAllAlgos = false;

        //short[] AlgoIds = new short[] { 11, 12, 13, 14, 15, 17, 18, 19, 30, 31, 32, 40, 41, 42, 50 };
        //double[] MktDirectionParams = new[] { 0.4, 0.5, 0.6, 0.3, 0.2, 0.1 };
        //double[] MktDirectionParams = new[] { 0.6 };//0.4, 0.5, 0.6, 0.3, 0.7}; 

        short[] AlgoIds = new short[] {  42 };
        //short[] AlgoIds = new short[] { 152};//150, 151, 152, 153 };
        ////double[] MktDirectionParams = new[] { 1, 2, 0.5, 1.5, 2.5, 3 };
        //double[] MktDirectionParamsOPT = new[] { 4, 5.0, 6, 7, 8, 9, 10, 11, 7.05, 7.15 };


        //short[] AlgoIds = new short[] { 13, 14, 15, 17, 18, 19, 20, 21, 22, 23, 30, 31, 32, 40, 41, 42, 50, 51, 52, 53, 60, 61, 62,
        //100, 101, 102, 113, 114, 115, 116, 117, 118, 119};
        //double[] MktDirectionParams = new[] { 0.6, 0.4, 0.5, 0.3, 0.7 };
        double[] MktDirectionParams = new[] { 0.3, 0.7, 1.0, 0.1 };

        // Beyond 0.6 and less than 0.3 does not work good, at least surely in nifty
        // Stoploss 1 is most optimized
        // Min profit 0.5, 0.75 and 1 are optimized. Not needed to dabble with them

        public static TradingAlgo GetAlgo(short algoId, AlgoParams algoParams, bool onlyCheckAlgoExist)//bool doLimitLoss = false)
        {

            TradingAlgo algo = null;
            bool isAlgoFound = true;
            //algoParams.AllowInitialTickStabilization = true;


            switch (algoId)
            {
                // -------------------- New Experiments Scalper 150 Algos-------------------------------- //
                case 150:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.5;
                    break;
                case 151:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 1;
                    break;
                case 152:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.75;
                    break;
                case 153:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.3;
                    break;

                // --------------------Scalper Algos: Immediate squareoff on min profit. No peak or minmax reset-------------------------------- //
                case 13:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.5;
                    algoParams.IsFixedTradesPerDay = true;
                    algoParams.NumTradesStopForDay = 2;
                    break;
                case 14:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 1;
                    algoParams.IsFixedTradesPerDay = true;
                    algoParams.NumTradesStopForDay = 2;
                    break;
                case 15:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.75;
                    algoParams.IsFixedTradesPerDay = true;
                    algoParams.NumTradesStopForDay = 2;
                    break;

                case 17:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.5;
                    break;
                case 18:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 1;
                    break;
                case 19:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.75;
                    break;

                // --------------------Scalper Algos: Immediate squareoff on min profit. Reset peaktype and minmax-------------------------------- //
                case 20:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.5;
                    break;
                case 21:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 1;
                    break;
                case 22:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.75;
                    break;

                case 23:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.35;
                    break;

                // ----------------------Min Profit must. threshold moves (perc-minprofit) ---------------------------------- //
                case 30:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.5;
                    break;
                case 31:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 1;
                    break;
                case 32:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.75;
                    break;
                // ---------------------Min profit + half of over that is must. threshold moves (perc-minprofit)/2------------------------------------------- //
                case 40:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.5;
                    break;
                case 41:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 1;
                    break;
                case 42:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.75;
                    break;

                // -------------------------Same like 40s with reset min-max--------------------------- //
                case 50:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.5;
                    break;
                case 51:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.75;
                    break;
                case 52:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 1;
                    break;

                case 53:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.35;
                    break;

                case 60:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.5;
                    algoParams.IsFixedTradesPerDay = true;
                    algoParams.NumTradesStopForDay = 1;
                    break;
                case 61:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.5;
                    algoParams.IsFixedTradesPerDay = true;
                    algoParams.NumTradesStopForDay = 2;
                    break;
                case 62:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.5;
                    algoParams.IsFixedTradesPerDay = true;
                    algoParams.NumTradesStopForDay = 3;
                    break;

                // ------------------ Stop loss threshold moves with profit : lenient------------------------- //
                case 100:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.5;
                    break;
                case 101:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 1;
                    break;
                case 102:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.75;
                    break;

                case 113:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.5;
                    algoParams.IsFixedTradesPerDay = true;
                    algoParams.NumTradesStopForDay = 2;
                    break;
                case 114:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 1;
                    algoParams.IsFixedTradesPerDay = true;
                    algoParams.NumTradesStopForDay = 2;
                    break;
                case 115:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.75;
                    algoParams.IsFixedTradesPerDay = true;
                    algoParams.NumTradesStopForDay = 2;
                    break;

                // ------------------ Stop loss threshold moves with profit :Straight profit/3 lenient------------------------- //
                case 116:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.5;
                    break;

                case 117:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 1;
                    break;

                case 118:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.75;
                    break;

                case 119:
                    algoParams.PercSquareOffThreshold = 1;
                    algoParams.PercMinProfit = 0.35;
                    algoParams.IsFixedTradesPerDay = true;
                    algoParams.NumTradesStopForDay = 2;
                    break;

                default:
                    isAlgoFound = false;
                    break;

            }

            // Temporary test
            //if (algoParams.IsReplayMode)
            //{
            //    //algoParams.PercSquareOffThreshold = 1;
            //    //algoParams.AllowInitialTickStabilization = true;
            //    //algoParams.IsFixedTradesPerDay = true;
            //    algoParams.IsLimitLossPerDay = true;
            //    algoParams.NumTradesStopForDay = 2;
            //    algoParams.PercSquareOffThreshold = 1.25;
            //}


            if (!isAlgoFound)
                return null;
            else if (onlyCheckAlgoExist)
            {
                algoParams.I = new Instrument("dummy", InstrumentType.FutureIndex, 100);
                if (algoParams.IsHedgeAlgo)
                    algo = new AlgoHedge(algoParams);
                else
                    algo = new AlgoScalper(algoParams);

                return algo;
            }

            algoParams.IsLimitLossPerDay = true;
            algoParams.PercLossStopForDay = 2.5;
            algoParams.NumNettLossTradesStopForDay = 5;

            if (algoId >= 10)
                algoParams.IsMinProfitMust = true;

            if (StockUtils.IsInstrumentOptionType(algoParams.I.InstrumentType))
            {
                algoParams.PercMinProfit += 5;
                if (algoParams.PercMarketDirectionChange == 7.05)
                    algoParams.PercSquareOffThreshold += 5;
                else if (algoParams.PercMarketDirectionChange == 7.15)
                {
                    //algoParams.PercMarketDirectionChange += 3.6;
                    algoParams.PercSquareOffThreshold += 15;
                }
                else
                    algoParams.PercSquareOffThreshold += 10;
                algoParams.PercBrokerage = 12000 / (algoParams.I.StrikePrice * algoParams.I.Qty);
                //algoParams.SquareOffBrokerageFactor = 1.3;
            }

            //if (algoId < 10)
            //    algo = new AlgoMinMax(algoParams);
            //else
            if (algoParams.IsHedgeAlgo)
                algo = new AlgoHedge(algoParams);
            else
                algo = new AlgoScalper(algoParams);

            return algo;
        }

        // IEOD - with Only LTP files (and no re-run) algo
        public void RunAlgos(object obj)
        {
            int symbolCounter = 0;
            var sts = new List<SymbolTick>(10000);

            // Get algo data
            var brokerAccountObj = (BrokingAccountObject)obj;
            IBroker broker = brokerAccountObj.Broker;
            var algoMetadata = (AlgoMetadata)brokerAccountObj.CustomData;
            AddTickChartDelegate dataDelegate = algoMetadata.DataDelegate;
            var chartDelegate = algoMetadata.ChartDelegate;
            var logDelegate = algoMetadata.LogDelegate;
            var tickMetaData = algoMetadata.TickMetadata;
            string r1, r2;
            var isForceUpdate = algoMetadata.IsForceUpdate;
            var algoParams = algoMetadata.AlgoParams;

            var AlgoIdSet = AlgoIds;

            if (DoAllAlgos)
            {
                short minAlgo = 10;
                short maxAlgo = 160;
                AlgoIdSet = new short[maxAlgo - minAlgo + 1];
                for (short i = minAlgo; i <= maxAlgo; i++)
                    AlgoIdSet[i - minAlgo] = i;

                //AlgoIdSet[maxAlgo - minAlgo + 1] = 1;
            }

            // Go for each symbol
            foreach (var tmd in tickMetaData)
            {
                r1 = tmd.R1;
                r2 = tmd.R2;
                var symbol = tmd.Symbol;
                var tickFileName = tmd.Path;


                logDelegate(string.Format("Thread {0}: Time: {4} Starting Stock: {1} r1: {2} r2: {3}\n",
                                          Thread.CurrentThread.ManagedThreadId, symbol, r1, r2, DateTime.Now));

                // CHECK TICKFILE EXISTS
                if (!File.Exists(tickFileName))
                {
                    Logger.LogWarningMessage(string.Format("TickFile {0} not found.", tickFileName));
                    continue;
                }

                // Check if ALGO results already exist (or need to be overwritten) PERIODWISE (r1, r2 combo).Checking last mktDir
                // If this goes past then it means need to run at least 1 mktDirPerc
                if (!isForceUpdate)
                {
                    bool doesEOPExist = true;
                    foreach (var algoId in AlgoIdSet)
                    {
                        if (GetAlgo(algoId, algoParams.Clone(), true) == null) continue;
                        // if trader run is already present for this symbol, then just move to next symbol
                        doesEOPExist = doesEOPExist && EOPTradeStats.DoesExistEOPStatsForMinMaxAlgos(symbol, algoId,
                            MktDirectionParams[MktDirectionParams.Length - 1], r1, r2);
                        if (!doesEOPExist)
                            break;
                    }
                    if (doesEOPExist)
                        continue;
                }

                ++symbolCounter;

                sts.Clear();
                GC.Collect();

                // Go through Each day's data for given symbol
                logDelegate(string.Format("Thread {0}: Stock: {1} " +
                                          "Time: {2} , Reading tickfile\n", Thread.CurrentThread.ManagedThreadId, symbol,
                                          DateTime.Now));

                Dictionary<int, Dictionary<int, SymbolTick>> TicksCall = new Dictionary<int, Dictionary<int, SymbolTick>>(10);
                Dictionary<int, Dictionary<int, SymbolTick>> TicksPut = new Dictionary<int, Dictionary<int, SymbolTick>>(10);

                DateTime expiryDate = DateTime.Now;
                int lotSize = 50;
                InstrumentType instrType = InstrumentType.OptionCallIndex;
                Instrument instr = null;

                if (algoParams.IsHedgeAlgo)
                {
                    Dictionary<int, Dictionary<int, SymbolTick>> dict;
                    //OPT-CNXBAN-26-Jul-2012-10500-CE-Q25-0915
                    var dir = Path.GetDirectoryName(tickFileName);
                    var allTickFiles = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories);
                    var allSymbolOptFiles = Directory.EnumerateFiles(dir, "OPT-" + symbol + "*", SearchOption.AllDirectories);

                    if (allSymbolOptFiles.Count() == 0)
                        continue;

                    foreach (string filePath in allTickFiles)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(filePath);
                        if (!fileName.StartsWith("OPT-" + symbol)) continue;

                        var fileParts = fileName.Split('-');

                        expiryDate = DateTime.Parse(fileParts[2] + "-" + fileParts[3] + "-" + fileParts[4]);
                        lotSize = int.Parse(fileParts[7].Replace("Q", ""));
                        var strkPrc = int.Parse(fileParts[5]);

                        if (fileParts[6] == "CE")
                        {
                            instrType = InstrumentType.OptionCallIndex;
                            dict = TicksCall;
                        }
                        else
                        {
                            instrType = InstrumentType.OptionPutIndex;
                            dict = TicksPut;
                        }
                        instr = new Instrument(symbol, instrType, strkPrc, expiryDate, lotSize);

                        if (!dict.ContainsKey(strkPrc))
                            dict.Add(strkPrc, new Dictionary<int, SymbolTick>(400));

                        var prcStore = dict[strkPrc];

                        var arrOpt = File.ReadAllLines(filePath);

                        int tickCnt = 0;
                        foreach (var s in arrOpt)
                        {
                            var dqi = ParseTickString(s, tmd.TickFormatType);

                            // Add to list of DQI 
                            if (dqi == null) continue;

                            dqi.InstrumentType = instrType;
                            dqi.UnderlyingSymbol = symbol;
                            dqi.ExpiryDateTime = expiryDate;
                            dqi.StrikePriceDouble = strkPrc;
                            var si = new SymbolTick(true);
                            si.D.Q = dqi;
                            si.I = instr;
                            tickCnt++;
                            var datetime = si.D.Q.QuoteTime;
                            int time = datetime.Hour * 100 + datetime.Minute;

                            if (!prcStore.ContainsKey(time))
                                prcStore.Add(time, si);
                        }
                    }
                }


                // READ TICKFILE
                var arr = File.ReadAllLines(tickFileName);
                double ltp = 0;
                int tickCount = 0;
                instrType = InstrumentType.FutureIndex;
                instr = new Instrument(symbol, instrType, 0, expiryDate, lotSize);
                foreach (var s in arr)
                {
                    var dqi = ParseTickString(s, tmd.TickFormatType);

                    // Add to list of DQI 
                    if (dqi == null) continue;

                    dqi.InstrumentType = instrType;
                    dqi.UnderlyingSymbol = symbol;
                    dqi.ExpiryDateTime = expiryDate;
                    // Add only stablemarket ticks, i.e. after inital 5 minutes. Start from 10 AM for stable data
                    //if (MarketUtils.IsTimeAfter10AM(dqi.UpdateTime))
                    {
                        var si = new SymbolTick(true);
                        si.D.Q = dqi;
                        si.I = instr;
                        sts.Add(si);
                        ltp += dqi.LastTradedPriceDouble;
                        tickCount++;
                    }
                }

                // Derive the quantity for FNO
                int qty = 1;
                double avgLotValue = 300000;
                if (tickCount != 0 && ltp != 0)
                {
                    ltp = ltp / tickCount;
                    qty = (int)Math.Round(avgLotValue / ltp);
                }

                foreach (var algoId in AlgoIdSet)
                {
                    if (GetAlgo(algoId, algoParams.Clone(), true) == null) continue;

                    // This is ALGO wise check. (for a given symbol, r1, r2) Check by last mktDirPerc
                    if (!isForceUpdate)
                    {
                        // if trader run is already present for this symbol, then just move to next symbol
                        var doesEOPExist = EOPTradeStats.DoesExistEOPStatsForMinMaxAlgos(symbol, algoId,
                            MktDirectionParams[MktDirectionParams.Length - 1], r1, r2);
                        if (doesEOPExist)
                        {
                            //Logger.LogWarningMessage(
                            //    string.Format(
                            //        "EOP TradeRun Stats for symbol={0}, algo={1} already exist. Skipping FULL symbol.",
                            //        symbol, algoId));
                            continue;
                        }
                    }

                    foreach (var mktDirecparam in MktDirectionParams)
                    {
                        var percMarketDirectionChange = Math.Round(mktDirecparam, 2);

                        // Special for 1 sec data
                        //if (percMarketDirectionChange == 0.1)// || percMarketDirectionChange == 0.5)
                        //    if (!(symbol == "NIFTY" || symbol == "DEFTY" || symbol == "SENSEX"))
                        //        continue;


                        // MktDirPerc wise check for a given symbol, algo, r1 and r2
                        if (!isForceUpdate)
                        {
                            // if trader run is already present for this symbol, then just move to next symbol
                            bool doesEOPExist = EOPTradeStats.DoesExistEOPStatsForMinMaxAlgos(symbol, algoId,
                                                                                              percMarketDirectionChange,
                                                                                              r1, r2);
                            if (doesEOPExist)
                            {
                                //Logger.LogWarningMessage(
                                //    string.Format(
                                //        "EOP TradeRun Stats for symbol={0}, algo={1}, marketDirection={2} already exist. Skipping for this combination.",
                                //        symbol, algoId, percMarketDirectionChange));
                                continue;
                            }
                        }

                        logDelegate(string.Format("Thread {0} Time {1} ~ Stock: {2}, R1: {3}, Running {4} @ {5}%\n",
                                                  Thread.CurrentThread.ManagedThreadId,
                                                  DateTime.Now,
                                                  symbol,
                                                  r1,
                                                  algoId,
                                                  percMarketDirectionChange));

                        // RUN ALGO
                        var algoParamsClone = algoParams.Clone();
                        algoParamsClone.PercMarketDirectionChange = percMarketDirectionChange;
                        algoParamsClone.AlgoId = algoId;
                        algoParamsClone.R1 = r1;
                        algoParamsClone.R2 = r2;
                        if (symbol.Contains("OPT-"))
                            algoParamsClone.I = new Instrument(symbol, InstrumentType.OptionCallIndex, ltp, new DateTime(2013, 03, 28), 50);
                        else
                            algoParamsClone.I = new Instrument(symbol, InstrumentType.FutureStock, 100, new DateTime(2013, 03, 28), qty);

                        algoParamsClone.PositionsFile = SystemUtils.GetStockRunTempFileName("Positions-" + algoParamsClone.Description());
                        algoParamsClone.StateFile = SystemUtils.GetStockRunTempFileName("AlgoState-" + algoParamsClone.Description());

                        TradingAlgo algo = GetAlgo(algoId, algoParamsClone, false);

                        algo.Prolog();

                        if (algoParams.IsHedgeAlgo)
                        {
                            algo.S.TicksCall = TicksCall;
                            algo.S.TicksPut = TicksPut;
                        }

                        //int tickCntr1Min = 0;
                        // Run for each quote tick now out of all collected from the replay file
                        foreach (var st in sts)
                        {
                            //if (!MarketUtils.IsTimeAfter10AM(dqi.UpdateTime))
                            //{
                            //    continue;
                            //}


                            //tickCntr1Min++;
                            try
                            {
                                if (algo.DoStopAlgo)
                                {
                                    logDelegate(string.Format("Thread {0}: Stock: {1} " +
                                                 "Time: {2} , Coming out due to Stop loss/NumTrades for the day\n", Thread.CurrentThread.ManagedThreadId, symbol,
                                                 DateTime.Now));
                                    break;
                                }
                                //if (algo.AlgoParams.AlgoId >= 120 && algo.AlgoParams.AlgoId < 150)
                                //{
                                //    if (tickCntr1Min % 5 == 0)
                                //    {
                                //        algo.S.TotalTickCount++;
                                //        algo.AddTick(st);
                                //    }
                                //    continue;
                                //}

                                algo.S.TotalTickCount++;
                                algo.AddTick(st);
                            }
                            catch (Exception ex)
                            {
                                FileTracing.TraceOut("Exception in algo.AddTick(): " + ex.Message + "\n" + ex.StackTrace);
                            }

                            algo.LTP = st.GetLTP();
                        }

                        algo.Epilog();

                        //EventResetChart(null, null);

                        // RESET Time in Ticks
                        //foreach (var st in sts)
                        //    st.D.Q.UpdateTime = st.D.Q.QuoteTime;

                    } // foreach algo param
                } // foreach algo
            } //foreach symbol

            logDelegate(string.Format("Thread {0} completed !! at Time: {1} \n", Thread.CurrentThread.ManagedThreadId,
                                      DateTime.Now));
        }

        #region SupportingMethods
        public static event ResetChartHandler EventResetChart;

        public static DerivativeSymbolQuote ParseTickString(string s, TickFormatType tickFormat)
        {
            if (string.IsNullOrEmpty(s))
                return null;

            DerivativeSymbolQuote dqi = null;

            try
            {
                if (tickFormat == TickFormatType.IEOD)
                {
                    //ABB,20091007,09:55:07,786.10,786.10,786.10,786.10,0,0
                    string[] parts = s.Split(',');

                    IFormatProvider culture = new CultureInfo("en-US", true);
                    string dateString = parts[1] + ":" + parts[2];
                    DateTime dt = DateTime.ParseExact(dateString, "yyyyMMdd:HH:mm:ss", culture);

                    //string tempStr = parts[1].Insert(4, "-").Insert(7, "-");
                    //DateTime lastTradeTime = DateTime.Parse(tempStr);
                    //tempStr = parts[2];

                    //lastTradeTime += TimeSpan.Parse(tempStr);

                    double ltp = double.Parse(parts[3]);
                    //double bid = double.Parse(parts[4]);
                    //double offer = double.Parse(parts[5]);

                    // default 1000 offer and bid qty and volume traded as 0

                    //dqi = new DerivativeQuoteInformation(dt, LTP, bid, offer, 1000, 1000, 0);

                    dqi = new DerivativeSymbolQuote(dt, ltp, ltp, ltp, 1000, 1000, 0);


                }
                else if (tickFormat == TickFormatType.Custom)
                {
                    //20090910:10:40:59;273.4;273.3;273.45;2500;2500;0
                    string[] parts = s.Split(';');

                    IFormatProvider culture = new CultureInfo("en-US", true);
                    DateTime dt = DateTime.ParseExact(parts[0], "yyyyMMdd:HH:mm:ss", culture);
                    int volumeTraded = 0;
                    if (parts.Length == 7)
                    {
                        volumeTraded = int.Parse(parts[6]);
                    }

                    dqi = new DerivativeSymbolQuote(
                        dt,
                        double.Parse(parts[1]),
                        double.Parse(parts[2]),
                        double.Parse(parts[3]),
                        int.Parse(parts[4]),
                        int.Parse(parts[5]),
                        volumeTraded
                        );
                }
                else if (tickFormat == TickFormatType.EOD)
                {
                    // Date,Open,High,Low,Close,Volume,Adj Close
                    // 2011-04-13,675.00,694.30,665.75,687.75,743800,687.75

                    try
                    {
                        string[] parts = s.Split(',');

                        IFormatProvider culture = new CultureInfo("en-US", true);
                        DateTime dt = DateTime.ParseExact(parts[0], "yyyy-MM-dd", culture);

                        double ltp = double.Parse(parts[1]); // Some close prices are incorrect in EOD data files, so consider open price
                        double close = double.Parse(parts[4]); // close (adj close is highly variant and confusing)

                        dqi = new DerivativeSymbolQuote(
                            dt,
                            ltp,
                            ltp,
                            close,
                            1000,
                            1000,
                            0
                            );
                    }
                    catch { }
                }
                else if (tickFormat == TickFormatType.OnlyLTP)
                {

                    // Date,LTP
                    // 20090910:10:40:59,273.4

                    try
                    {
                        string[] parts = s.Split(',');

                        IFormatProvider culture = new CultureInfo("en-US", true);
                        DateTime dt = DateTime.ParseExact(parts[0], "yyyyMMdd:HH:mm:ss", culture);

                        double ltp = double.Parse(parts[1]);

                        dqi = new DerivativeSymbolQuote(
                            dt,
                            ltp,
                            ltp,
                            ltp,
                            1000,
                            1000,
                            0
                            );
                    }
                    catch { }
                }

            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

            return dqi;
        }
        #endregion
    }
}


