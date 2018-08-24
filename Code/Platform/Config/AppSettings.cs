
//using StockTrader.Platform.Database;

using System;
using System.Configuration;
using System.Globalization;
using StockTrader.Core;
using StockTrader.Platform.Logging;


namespace StockTrader.Config
{
    // This class will be used to manage all web config keys. 
    // It will read the keys and store the values as read-only statics .
    // Other code modules should not read any key on their own , they should use this class to get config values

    // Currently it is a Skeleton , need to pull config params here after 1st production release.

    public static class AppSettings
    {
        public static bool GetConfigFor_StockTrader(out ProgramMKDConfig mkdConfig)
        {
            string traceString = null;
            mkdConfig = new ProgramMKDConfig();

            try
            {
                mkdConfig.mock = bool.Parse(ConfigurationManager.AppSettings["BuySellAnalyzer_MOCK"]);
                traceString += string.Format("MOCK = {0}\n", mkdConfig.mock);

                mkdConfig.bIsReplayMode = bool.Parse(ConfigurationManager.AppSettings["BuySellAnalyzer_RunInReplayMode"]);
                traceString += string.Format("IsReplayMode = {0}\n", mkdConfig.bIsReplayMode);

                mkdConfig.replayTickFile = null;
                if (mkdConfig.bIsReplayMode)
                {
                    mkdConfig.replayTickFile = ConfigurationManager.AppSettings["BuySellAnalyzer_ReplayTickFile"];
                    traceString += string.Format("ReplayTickFile = {0}\n", mkdConfig.replayTickFile);
                }

                mkdConfig.expiryDate = DateTime.ParseExact(ConfigurationManager.AppSettings["BuySellAnalyzer_Expiry"], "yyyy-MM-dd", DateTimeFormatInfo.InvariantInfo);
                traceString += "ExpiryDate = " + mkdConfig.expiryDate.ToLongDateString() + "\n";

                mkdConfig.stockCode = ConfigurationManager.AppSettings["BuySellAnalyzer_StockCode"];
                traceString += "StockCode = " + mkdConfig.stockCode + "\n";

                mkdConfig.positionsFile = "C:\\StockTraderStateFiles\\S.OpenPositions-" + mkdConfig.stockCode + ".txt";
                traceString += string.Format("OpenPositionsStatusFile = {0}\n", mkdConfig.positionsFile);

                mkdConfig.brokerage = double.Parse(ConfigurationManager.AppSettings["BuySellAnalyzer_Brokerage"]);
                traceString += string.Format("brokerage = {0}\n", mkdConfig.brokerage);

                mkdConfig.instType = (InstrumentType)Enum.Parse(typeof(InstrumentType), ConfigurationManager.AppSettings["BuySellAnalyzer_Instrument"]);
                traceString += "InstrumentType = " + mkdConfig.instType.ToString() + "\n";

                mkdConfig.quantity = ConfigurationManager.AppSettings["BuySellAnalyzer_Quantity"];
                traceString += string.Format("TradeQuantity = {0}\n", mkdConfig.quantity);

                mkdConfig.percMarketDirectionChange = double.Parse(ConfigurationManager.AppSettings["BuySellAnalyzer_PercentageChangeForMarketDirection"]);
                mkdConfig.percSquareOffThreshold = double.Parse(ConfigurationManager.AppSettings["BuySellAnalyzer_PercChangeThresholdForSquareOff"]);
                //mkdConfig.percentageChangeForFreshBuy = double.Parse(ConfigurationManager.AppSettings["BuySellAnalyzer_PercentageChangeForFreshBuy"]);
                //mkdConfig.percentageChangeForShortSquareOff = double.Parse(ConfigurationManager.AppSettings["BuySellAnalyzer_PercentageChangeForShortSquareOff"]);
                //mkdConfig.percentageChangeForFreshShort = double.Parse(ConfigurationManager.AppSettings["BuySellAnalyzer_PercentageChangeForFreshShort"]);
                //mkdConfig.percentageChangeForLongSquareOff = double.Parse(ConfigurationManager.AppSettings["BuySellAnalyzer_PercentageChangeForLongSquareOff"]);

                traceString += string.Format("percMarketDirectionChange = {0}\n", mkdConfig.percMarketDirectionChange);
                traceString += string.Format("percSquareOffThreshold = {0}\n", mkdConfig.percSquareOffThreshold);
                //traceString += string.Format("percentageChangeForFreshBuy = {0}\n", mkdConfig.percentageChangeForFreshBuy);
                //traceString += string.Format("percentageChangeForShortSquareOff = {0}\n", mkdConfig.percentageChangeForShortSquareOff);
                //traceString += string.Format("percentageChangeForFreshShort = {0}\n", mkdConfig.percentageChangeForFreshShort);
                //traceString += string.Format("percentageChangeForLongSquareOff = {0}\n", mkdConfig.percentageChangeForLongSquareOff);

                mkdConfig.minProfitPerc = double.Parse(ConfigurationManager.AppSettings["BuySellAnalyzer_MinProfitPerc"]);
                traceString += string.Format("minProfitPerc = {0}\n", mkdConfig.minProfitPerc);

                mkdConfig.stopLossPerc = double.Parse(ConfigurationManager.AppSettings["BuySellAnalyzer_StopLossPerc"]);
                traceString += string.Format("stopLossPerc = {0}\n", mkdConfig.stopLossPerc);

                mkdConfig.percPositionSpacing = double.Parse(ConfigurationManager.AppSettings["BuySellAnalyzer_PositionSpacingPerc"]);
                Console.WriteLine("percPositionSpacing = " + mkdConfig.percPositionSpacing);

                mkdConfig.maxTotalPositions = int.Parse(ConfigurationManager.AppSettings["BuySellAnalyzer_MaxTotalPositions"]);
                mkdConfig.maxLongPositions = int.Parse(ConfigurationManager.AppSettings["BuySellAnalyzer_MaxLongPositions"]);
                mkdConfig.maxShortPositions = int.Parse(ConfigurationManager.AppSettings["BuySellAnalyzer_MaxShortPositions"]);

                traceString += string.Format("maxTotalPositions = {0}\n", mkdConfig.maxTotalPositions);
                traceString += string.Format("maxLongPositions = {0}\n", mkdConfig.maxLongPositions);
                traceString += string.Format("maxShortPositions = {0}\n", mkdConfig.maxShortPositions);

                mkdConfig.longCeilingPrice = double.Parse(ConfigurationManager.AppSettings["BuySellAnalyzer_LongCeilingPrice"]);
                traceString += string.Format("longCeilingPrice = {0}\n", mkdConfig.longCeilingPrice);

                mkdConfig.shortFloorPrice = double.Parse(ConfigurationManager.AppSettings["BuySellAnalyzer_ShortFloorPrice"]);
                traceString += string.Format("shortFloorPrice = {0}\n", mkdConfig.shortFloorPrice);


                mkdConfig.useProbableTradeValue = bool.Parse(ConfigurationManager.AppSettings["BuySellAnalyzer_UseProbableTradeValue"]);

                string str_LongShort = ConfigurationManager.AppSettings["BuySellAnalyzer_LongOrShort"];
                if (str_LongShort != "S" && str_LongShort != "L" && str_LongShort != "LS" && str_LongShort != "SL")
                {
                    traceString += "Invalid value for str_LongShort";
                    FileTracing.TraceOut(traceString);
                    return false;
                }

                mkdConfig.allowShort = (str_LongShort == "S") || (str_LongShort == "LS") || (str_LongShort == "SL");
                mkdConfig.allowLong = (str_LongShort == "L") || (str_LongShort == "LS") || (str_LongShort == "SL");
                traceString += string.Format("allowShort = {0}\n", mkdConfig.allowShort);
                traceString += string.Format("allowLong = {0}\n", mkdConfig.allowLong);



                //bool bIsInitialOrder = bool.Parse(ConfigurationManager.AppSettings["BuySellAnalyzer_IsInitialOrder"]);

                //Order initialOrder = null;
                //if (bIsInitialOrder)
                //{
                //    {
                //        string str_BuyPos = ConfigurationManager.AppSettings["BuySellAnalyzer_InitialOrder_OrderPosition"];
                //        double db_Price = -1;
                //        if (double.TryParse(ConfigurationManager.AppSettings["BuySellAnalyzer_InitialOrder_OrderPrice"], out db_Price) &&
                //            (!string.IsNullOrEmpty(str_BuyPos)))
                //        {
                //            Position position = (Position)Enum.Parse(typeof(Position), str_BuyPos);
                //            mkdConfig.initialOrder = new Order(position, db_Price);
                //        }
                //        else
                //        {
                //            mkdConfig.initialOrder = new Order(Position.None, 0);
                //        }
                //    }
                //}
                //else
                //{
                //    mkdConfig.initialOrder = new Order(Position.None, 0);
                //}
                //traceString += bIsInitialOrder ? ("InitialOrder = " + mkdConfig.ToString()) : "No Initial Order" + "\n";

                FileTracing.TraceOut(traceString);
            }
            catch (Exception e)
            {
                FileTracing.TraceOut("Exception: " + e);
                return false;
            }

            return true;
        }

        public static bool GetConfig(out ProgramConfigParams config)
        {
            config = new ProgramConfigParams();
            string traceString;
            try
            {
                config.username = ConfigurationManager.AppSettings["IciciDirect_Username"];
                config.password = ConfigurationManager.AppSettings["IciciDirect_Password"];
                config.bRunStockTrader = bool.Parse(ConfigurationManager.AppSettings["RunStockTrader"]);
                config.bRunTillMarketOpen = bool.Parse(ConfigurationManager.AppSettings["RunTillMarketOpen"]);
                config.runTimeInMinutes = double.Parse(ConfigurationManager.AppSettings["ProgramRunTime"]);
                config.bProgramRemoteControl = bool.Parse(ConfigurationManager.AppSettings["ProgramRemoteControl"]);
                config.numStocks = UInt32.Parse(ConfigurationManager.AppSettings["NumStocks"]);
                config.fundsStartingLimitAccount = double.Parse(ConfigurationManager.AppSettings["FundsStartingLimitAccount"]);
                config.lossLimitAccount = double.Parse(ConfigurationManager.AppSettings["LossLimitAccount"]);

                traceString = "\n----------- Program run configuration ----------\n";
                traceString += string.Format("Run till Market Open = {0}\n", config.bRunTillMarketOpen ? bool.TrueString : bool.FalseString);
                traceString += string.Format("Run time of Program = {0}\n", config.runTimeInMinutes);
                traceString += string.Format("Remote Program Control = {0}\n", config.bProgramRemoteControl ? bool.TrueString : bool.FalseString);
                traceString += string.Format("Run StockTrader = {0}\n", config.bRunStockTrader);
                traceString += string.Format("Number of Stocks in System = {0}\n", config.numStocks);
                traceString += "\n----------- Account-wide Configuration ----------\n";
                traceString += string.Format("Account-wide starting FundLimit = {0}\n", config.fundsStartingLimitAccount);
                traceString += string.Format("Account-wide loss Limit set = {0}\n", config.lossLimitAccount);
                traceString += "\n----------- Stock Specific Configuration ----------\n";

                config.stockParams = new ProgramStockParams[config.numStocks];
                for (int i = 0; i < config.numStocks; i++)
                {
                    ProgramStockParams stockParams = new ProgramStockParams();
                    config.stockParams[i] = stockParams;
                    string stockPrefix = "Stock" + i.ToString() + "_";
                    stockParams.stockCode = ConfigurationManager.AppSettings[stockPrefix + "StockCode"];
                    stockParams.stockTradingLot = int.Parse(ConfigurationManager.AppSettings[stockPrefix + "StockTradingLot"]);
                    stockParams.bStartAFresh = bool.Parse(ConfigurationManager.AppSettings[stockPrefix + "StartAFresh"]);
                    stockParams.bCancelExistingOrdersAtStart = bool.Parse(ConfigurationManager.AppSettings[stockPrefix + "CancelExistingOrdersAtStart"]);
                    stockParams.bCancelOutstandingOrdersAtEnd = bool.Parse(ConfigurationManager.AppSettings[stockPrefix + "CancelOutstandingOrdersAtEnd"]);
                    stockParams.stockAvailableStarting = int.Parse(ConfigurationManager.AppSettings[stockPrefix + "StockAvailableStarting"]);
                    stockParams.initBidPrice = double.Parse(ConfigurationManager.AppSettings[stockPrefix + "InitBidPrice"]);
                    stockParams.initOfferPrice = double.Parse(ConfigurationManager.AppSettings[stockPrefix + "InitOfferPrice"]);

                    traceString += "---------------------------------------------------------------\n";
                    traceString += "Stock: #" + i.ToString() + "\n";
                    traceString += string.Format("Stock code = {0}\n", stockParams.stockCode);
                    traceString += string.Format("Stock Trading lot = {0}\n", stockParams.stockTradingLot);
                    traceString += string.Format("Start Afresh = {0}\n", stockParams.bStartAFresh);
                    traceString += string.Format("Cancel existing outstanding orders at start= {0}\n", stockParams.bCancelExistingOrdersAtStart);
                    traceString += string.Format("Cancel outstanding orders at exit = {0}\n", stockParams.bCancelOutstandingOrdersAtEnd);
                    traceString += string.Format("Stock available in DP to sell at start = {0}\n", stockParams.stockAvailableStarting);
                    traceString += string.Format("InitBidPrice = {0}\n", stockParams.initBidPrice);
                    traceString += string.Format("InitOfferPrice = {0}\n", stockParams.initOfferPrice);
                    traceString += "---------------------------------------------------------------\n";

                }
                // Trace out the big config info string now at once
                // this contains all of APPCONFIG info
                FileTracing.TraceOut(traceString);
            }
            catch (Exception e)
            {
                FileTracing.TraceOut("Exception: " + e);
                return false;
            }

            return true;
        }
    }

    public class ProgramStockParams
    {
        public string stockPrefix;
        public string stockCode;
        public int stockTradingLot;
        public bool bStartAFresh;
        public bool bCancelExistingOrdersAtStart;
        public bool bCancelOutstandingOrdersAtEnd;
        public int stockAvailableStarting;
        public double initBidPrice;
        public double initOfferPrice;
    }

    public class ProgramConfigParams
    {
        public string username;
        public string password;
        public bool bRunStockTrader;
        public bool bRunTillMarketOpen;
        public double runTimeInMinutes;
        public bool bProgramRemoteControl;
        public uint numStocks;
        public double fundsStartingLimitAccount;
        public double lossLimitAccount;
        public ProgramStockParams[] stockParams;
    }

    public class ProgramMKDConfig
    {
        public double percMarketDirectionChange;
        public double percSquareOffThreshold;
        //public double percentageChangeForFreshBuy;
        //public double percentageChangeForShortSquareOff;
        //public double percentageChangeForFreshShort;
        //public double percentageChangeForLongSquareOff;
        public double stopLossPerc;
        public double minProfitPerc;
        public double percPositionSpacing;

        public int maxLongPositions;
        public int maxShortPositions;
        public int maxTotalPositions;

        public bool allowShort;
        public bool allowLong;
        public double longCeilingPrice;
        public double shortFloorPrice;

        public double brokerage;
        public string stockCode;
        public string quantity;
        public InstrumentType instType;
        public DateTime expiryDate;
        public bool mock;
        public bool bIsReplayMode;

        public bool useProbableTradeValue;

        public string replayTickFile;
        public string positionsFile;
        //public string mFileName;

        //public Order initialOrder;
    }
}                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          