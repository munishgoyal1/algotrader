using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using StockTrader.Brokers.UpstoxBroker;
using StockTrader.Core;
using StockTrader.Platform.Logging;
using StockTrader.Utilities;
using StockTrader.Utilities.Broker;
using UpstoxNet;

namespace UpstoxTrader
{
    class UpstoxProgram
    {
        private static Mutex mutex;

        static UpstoxProgram()
        {
            var filesPath = SystemUtils.GetStockFilesPath();
            string credsFilePath = Path.Combine(filesPath, "creds.txt");
            var credsLines = File.ReadAllLines(credsFilePath);

#if DEBUG
            userId = credsLines[0] + "_DEBUG";
#else
            userId = credsLines[0];
#endif
            apiKey = credsLines[1];
            apiSecret = credsLines[2];
            redirectUrl = credsLines[3];

            mutex = new Mutex(false, userId);
        }

        //Run logs - C:\StockRunFiles\TraderLogs\
        private static string userId;
        private static string apiKey;
        private static string apiSecret;
        private static string redirectUrl;


        [STAThread]
        static void Main(string[] args)
        {

            BrokerErrorCode errCode = BrokerErrorCode.Unknown;

            var upstoxBroker = new MyUpstoxWrapper(apiKey, apiSecret, redirectUrl);

#if DEBUG
            Trace("DEBUG MODE");
            List<UpstoxTradeParams> stocksConfig1 = ReadTradingConfigFile();

            errCode = upstoxBroker.Login1();
#else
            Trace("RELEASE MODE"); errCode = upstoxBroker.Login();
#endif
            // Check for Holiday today
            if (IsHolidayToday())
            {
                Trace("Exchange Holiday today.. exiting.");
                return;
            }

            // Wait 2 seconds if contended – in case another instance
            // of the program is in the process of shutting down.
            if (!mutex.WaitOne(TimeSpan.FromSeconds(2), false))
            {
                var message = "Another instance of the app is running. Only one instance for a userId is allowed! UserId: " + userId + ". Exiting..";
                Trace(message);
                return;
            }

            Trace(userId);

#if !DEBUG
            MarketUtils.WaitUntilMarketOpen();
#endif

            //Thread.Sleep(5000);// Let the rates etc update on server

            // Read the config file
            List<UpstoxTradeParams> stocksConfig = ReadTradingConfigFile();

            var threads = new List<Thread>(stocksConfig.Count);

            var mktTrendConfig = new UpstoxMarketTrendParams();


            var monthNow = DateTime.Now.Month + (DateTime.Now.Day > 20 ? 1 : 0);
            var yearNow = DateTime.Now.Year + (monthNow > 12 ? 1 : 0);
            if (monthNow > 12)
                monthNow = 1;

            var months = new[] { "DUMMY", "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };

            var niftyFutCode = string.Format("NIFTY{0}{1}FUT", (yearNow - 2000), months[monthNow]);

            mktTrendConfig.stockCode = niftyFutCode;
            mktTrendConfig.exchangeStr = "NSE_FO";
            mktTrendConfig.upstox = upstoxBroker;
            var mktTrend = new MarketTrend(mktTrendConfig);
            var mktThread = new Thread(mktTrend.StartCapturingMarketTrend);
            threads.Add(mktThread);

            foreach (var stockConfig in stocksConfig)
            {
                stockConfig.upstox = upstoxBroker;
                stockConfig.mktTrend = mktTrend;
                upstoxBroker.AddStock(stockConfig.stockCode);
                var t = new Thread(new UpstoxAverageTheBuyThenSell(stockConfig).StockBuySell);
                threads.Add(t);
            }

            threads.ForEach(t => { t.Start(); /*Thread.Sleep(200);*/ });
            threads.ForEach(t => t.Join());

            Trace("Update PnL files");
            WritePnL(upstoxBroker, stocksConfig);

            // Send out the log in email and chat
            Trace("All stock threads completed. Emailing today's log file");
            MessagingUtils.Init();
            var log = GetLogContent();
            MessagingUtils.SendAlertMessage("SimpleTrader log", log);
            Trace("Exiting..");
        }

        public static void WritePnL(MyUpstoxWrapper upstoxBroker, List<UpstoxTradeParams> stocksConfig)
        {
            try
            {
                // calculate stats
                Dictionary<string, EquityTradeBookRecord> tradeBook;

                var filesPath = SystemUtils.GetPnLFilesLocation();

                upstoxBroker.GetTradeBook(false, null, out tradeBook);
                Dictionary<string, List<EquityTradeBookRecord>> stockBook = tradeBook.Values.GroupBy(t => t.StockCode).ToDictionary(g => g.Key, g => g.ToList());

                string globalPnLFilePath = Path.Combine(filesPath, "global_pnl.txt");
                if (!File.Exists(globalPnLFilePath))
                {
                    var defaultSummary = string.Format("0,0,0,0,0,0,0,0,0,0");
                    File.WriteAllLines(globalPnLFilePath, new[] { defaultSummary });
                }

                var globalPnLLines = File.ReadAllLines(globalPnLFilePath);

                var globalPnLLine = globalPnLLines[0].Split(',');

                double globalnetrealized = double.Parse(globalPnLLine[1]);
                double globalbrokerage = double.Parse(globalPnLLine[6]);
                double globalIntradayValue = double.Parse(globalPnLLine[8]);
                double globalDeliveryValue = double.Parse(globalPnLLine[9]);


                double globalnetmtm = 0, globalnetunrealized = 0, globalnetinflow = 0, globalcurrentholdingatcost = 0;

                double globaltodaymtm = 0, globaltodayrealized = 0, globaltodayunrealized = 0, globaltodayinflow = 0, globaltodayholdingcost = 0, globalTodayIntradayValue = 0, globalTodayDeliveryValue = 0;

                double maxAmountCommittedToday = 0, pctPnLToday = 0;
                double avgAmountCommitted = 0, pctPnL = 0;

                double globalMaxAmountCommittedToday = 0, globalPctPnLToday = 0;
                double globalAvgAmountCommitted = 0, globalPctPnL = 0;
                double todaybrokerage = 0, globaltodaybrokerage = 0;

                foreach (var kv in stockBook)
                {
                    var stockCode = kv.Key;
                    var trades = kv.Value;
                    var stockConfig = stocksConfig.Find(c => c.stockCode == stockCode);
                    var stats = stockConfig.stats;

                    string pnlFilePath = Path.Combine(filesPath, stockCode + "_pnl.txt");

                    var configToday = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}", stockConfig.stockCode,
                            stockConfig.markDownPctForBuy, stockConfig.markDownPctForAveraging, stockConfig.sellMarkup,
                            stockConfig.placeBuyNoLtpCompare, stockConfig.startTime.ToString("hh:mm"), stockConfig.priceBucketWidthForQty, stockConfig.qtyAgressionFactor,
                            string.Join(":", stockConfig.priceBucketsForPrice), string.Join(";", stockConfig.priceBucketsForQty));

                    if (!File.Exists(pnlFilePath))
                    {
                        var defaultSummary = string.Format("0,0,0,0,0,0,0,0,0,0,0,0,0");
                        File.WriteAllLines(pnlFilePath, new[] { defaultSummary, configToday });
                    }

                    var todayBuyTrades = trades.Sum(t => t.Direction == OrderDirection.BUY ? 1 : 0);
                    var todaySellTrades = trades.Sum(t => t.Direction == OrderDirection.SELL ? 1 : 0);
                    var todayBuyQty = trades.Sum(t => t.Direction == OrderDirection.BUY ? t.Quantity : 0);
                    var todaySellQty = trades.Sum(t => t.Direction == OrderDirection.SELL ? t.Quantity : 0);
                    var todayBuyValue = trades.Sum(t => t.Direction == OrderDirection.BUY ? t.Quantity * t.Price : 0);
                    var todaySellValue = trades.Sum(t => t.Direction == OrderDirection.SELL ? t.Quantity * t.Price : 0);

                    var orderQty = stats.baseOrderQty;

                    // Get Ltp
                    double ltp;
                    DateTime lut;
                    upstoxBroker.GetEquityLTP(stockConfig.exchange == Exchange.NSE ? "NSE_EQ" : "BSE_EQ", stockCode, out ltp, out lut);
                    if (DateTime.Now - lut >= TimeSpan.FromMinutes(5))
                        ltp = 0;

                    // Get final holding position
                    double outstandingQty = 0;
                    double outstandingPrice = 0;
                    var positionFile = AlgoUtils.GetPositionFile(stockConfig.stockCode);
                    var positionLines = File.ReadAllLines(positionFile);
                    if (positionLines.Length > 0)
                        if (!string.IsNullOrEmpty(positionLines[0]))
                        {
                            var split = positionLines[0].Split(' ');
                            outstandingQty = int.Parse(split[0].Trim());
                            outstandingPrice = double.Parse(split[1].Trim());
                        }

                    var outstandingValue = outstandingQty * outstandingPrice;

                    var prevHoldingQty = stats.prevHoldingQty;
                    var prevHoldingPrice = stats.prevHoldingPrice;
                    var prevHoldingValue = prevHoldingQty * prevHoldingPrice;

                    var todayHoldingQty = outstandingQty - prevHoldingQty;
                    var todayHoldingPrice = todayHoldingQty != 0 ? (outstandingValue - prevHoldingValue) / todayHoldingQty : 0;
                    var todayHoldingValue = todayHoldingQty * todayHoldingPrice;

                    double todayrealized = todayHoldingValue > 0 ?
                        todaySellValue - (todayBuyValue - Math.Abs(todayHoldingValue))
                        : (todaySellValue - Math.Abs(todayHoldingValue)) - todayBuyValue;

                    var todayHoldingNetQty = Math.Max(todayHoldingQty, 0);

                    var todayIntradayValue = trades.Sum(t => t.EquityOrderType == EquityOrderType.MARGIN ? t.Quantity * t.Price : 0);
                    var todayDeliveryBuyValue = trades.Sum(t => t.Direction == OrderDirection.BUY && t.EquityOrderType == EquityOrderType.DELIVERY ? t.Quantity * t.Price : 0);
                    var todayDeliverySellValue = Math.Abs(trades.Sum(t => t.Direction == OrderDirection.SELL && t.EquityOrderType == EquityOrderType.DELIVERY ? t.Quantity * t.Price : 0));

                    var prevRemainingOutstandingValue = prevHoldingValue - todayDeliverySellValue;
                    var todayDeliveryConvertedValue = outstandingValue - prevRemainingOutstandingValue;
                    todayDeliveryConvertedValue = todayDeliveryConvertedValue > todayHoldingPrice / 2 ? todayHoldingPrice : 0; // to ignore small delta which means no real converted qty today

                    todayIntradayValue -= todayDeliveryConvertedValue;
                    todayDeliveryBuyValue += todayDeliveryConvertedValue;
                    var todayDeliveryValue = todayDeliveryBuyValue + todayDeliverySellValue;

                    todaybrokerage = 0.0005 * todayIntradayValue;
                    todaybrokerage += (0.0015 * todayDeliveryValue);

                    globalTodayIntradayValue += todayIntradayValue;
                    globalTodayDeliveryValue += todayDeliveryValue;
                    globaltodaybrokerage += todaybrokerage;

                    todayIntradayValue = Math.Round(todayIntradayValue);
                    todayDeliveryValue = Math.Round(todayDeliveryValue);

                    todayrealized -= todaybrokerage;
                    double todayunrealized = todayHoldingNetQty * (ltp - todayHoldingPrice);//today delivery mtm
                    double todaymtm = todayrealized + todayunrealized;
                    double todayholdingcost = todayHoldingNetQty * todayHoldingPrice;//only today delivery cost
                    double todayinflow = todayrealized - todayholdingcost;

                    var pnlLines = File.ReadAllLines(pnlFilePath);
                    var netPnLline = pnlLines[0].Split(',');

                    double netrealized = double.Parse(netPnLline[1]);
                    double brokerage = double.Parse(netPnLline[6]);
                    netrealized += todayrealized;
                    brokerage += todaybrokerage;
                    double netunrealized = ltp > 0 ? outstandingQty * (ltp - outstandingPrice) : 0;
                    double netmtm = netrealized + netunrealized;
                    double currentholdingatcost = outstandingQty * outstandingPrice;
                    double netinflow = netrealized - currentholdingatcost;

                    globaltodaymtm += todaymtm;
                    globaltodayrealized += todayrealized;
                    globaltodayunrealized += todayunrealized;
                    globaltodayinflow += todayinflow;
                    globaltodayholdingcost += todayholdingcost;

                    globalnetmtm += netmtm;
                    globalnetrealized += netrealized;
                    globalnetunrealized += netunrealized;
                    globalnetinflow += netinflow;
                    globalcurrentholdingatcost += currentholdingatcost;

                    double totalIntradayValue = double.Parse(netPnLline[11]);
                    double totalDeliveryValue = double.Parse(netPnLline[12]);

                    totalIntradayValue += todayIntradayValue;
                    totalDeliveryValue += todayDeliveryValue;

                    maxAmountCommittedToday = (stats.maxBuyValueToday * 0.1) + outstandingValue; //10% as margin money so it is 0.1 of the maxBuyValue
                    pctPnLToday = maxAmountCommittedToday != 0 ? (todaymtm / maxAmountCommittedToday) * 100 : 0;
                    var dayLines = pnlLines.Skip(1).Where(l => !l.StartsWith(stockCode));
                    avgAmountCommitted = (maxAmountCommittedToday + dayLines.Sum(d => double.Parse(d.Split(',')[6]))) / (dayLines.Count() + 1);
                    pctPnL = avgAmountCommitted != 0 ? (netmtm / avgAmountCommitted) * 100 : 0;

                    globalMaxAmountCommittedToday += maxAmountCommittedToday;
                    globalAvgAmountCommitted += avgAmountCommitted;

                    todaymtm = Math.Round(todaymtm);
                    todayrealized = Math.Round(todayrealized);
                    todayunrealized = Math.Round(todayunrealized);
                    todayinflow = Math.Round(todayinflow);
                    todayholdingcost = Math.Round(todayholdingcost);

                    netmtm = Math.Round(netmtm);
                    netrealized = Math.Round(netrealized);
                    netunrealized = Math.Round(netunrealized);
                    netinflow = Math.Round(netinflow);
                    currentholdingatcost = Math.Round(currentholdingatcost);

                    maxAmountCommittedToday = Math.Round(maxAmountCommittedToday);
                    pctPnLToday = Math.Round(pctPnLToday, 1);
                    avgAmountCommitted = Math.Round(avgAmountCommitted);
                    pctPnL = Math.Round(pctPnL, 1);

                    todaybrokerage = Math.Round(todaybrokerage);
                    brokerage = Math.Round(brokerage);

                    var lastConfigLine = pnlLines.Where(l => l.StartsWith(stockCode)).Last();

                    if (configToday != lastConfigLine)
                        File.AppendAllLines(pnlFilePath, new[] { configToday });

                    var readPnLLines = File.ReadAllLines(pnlFilePath);

                    readPnLLines[0] = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}", netmtm, netrealized, netunrealized, netinflow, currentholdingatcost,
                        avgAmountCommitted, brokerage, pctPnL,
                        outstandingQty, outstandingPrice, ltp, totalIntradayValue, totalDeliveryValue);

                    var summaryToday = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15}", DateTime.Today.ToString("dd-MM-yyyy"),
                        todaymtm, todayrealized, todayunrealized, todayinflow, todayholdingcost, maxAmountCommittedToday, todaybrokerage, pctPnLToday,
                        todayBuyTrades, todaySellTrades, Math.Round(orderQty,2), todayBuyQty, todaySellQty, todayIntradayValue, todayDeliveryValue);

                    var finalPnLLines = readPnLLines.ToList();
                    finalPnLLines.Add(summaryToday);
                    File.WriteAllLines(pnlFilePath, finalPnLLines);
                }

                var globaldayLines = globalPnLLines.Skip(1);
                var dayCount = globaldayLines.Count();
                globalPctPnLToday = globalMaxAmountCommittedToday != 0 ? (globaltodaymtm / globalMaxAmountCommittedToday) * 100 : 0;
                globalPctPnL = globalAvgAmountCommitted != 0 ? (globalnetmtm / globalAvgAmountCommitted) * 100 : 0;//* (365/(dayCount == 0 ? 1:dayCount))

                globalIntradayValue += globalTodayIntradayValue;
                globalDeliveryValue += globalTodayDeliveryValue;

                globalbrokerage += globaltodaybrokerage;

                globalTodayIntradayValue = Math.Round(globalTodayIntradayValue);
                globalTodayDeliveryValue = Math.Round(globalTodayDeliveryValue);
                globalIntradayValue = Math.Round(globalIntradayValue);
                globalDeliveryValue = Math.Round(globalDeliveryValue);

                globaltodaymtm = Math.Round(globaltodaymtm);
                globaltodayrealized = Math.Round(globaltodayrealized);
                globaltodayunrealized = Math.Round(globaltodayunrealized);
                globaltodayinflow = Math.Round(globaltodayinflow);
                globaltodayholdingcost = Math.Round(globaltodayholdingcost);
                globalMaxAmountCommittedToday = Math.Round(globalMaxAmountCommittedToday);

                globalnetmtm = Math.Round(globalnetmtm);
                globalnetrealized = Math.Round(globalnetrealized);
                globalnetunrealized = Math.Round(globalnetunrealized);
                globalnetinflow = Math.Round(globalnetinflow);
                globalcurrentholdingatcost = Math.Round(globalcurrentholdingatcost);
                globalAvgAmountCommitted = Math.Round(globalAvgAmountCommitted);

                globalPctPnLToday = Math.Round(globalPctPnLToday, 1);
                globalPctPnL = Math.Round(globalPctPnL, 1);

                globaltodaybrokerage = Math.Round(globaltodaybrokerage);
                globalbrokerage = Math.Round(globalbrokerage);


                //write global pnl
                globalPnLLines[0] = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}", globalnetmtm, globalnetrealized, globalnetunrealized, globalnetinflow,
                    globalcurrentholdingatcost, globalAvgAmountCommitted, globalbrokerage, globalPctPnL,
                    globalIntradayValue, globalDeliveryValue);

                var globalSummaryToday = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}", DateTime.Today.ToString("dd-MM-yyyy"),
                    globaltodaymtm, globaltodayrealized, globaltodayunrealized, globaltodayinflow, globaltodayholdingcost, globalMaxAmountCommittedToday, globaltodaybrokerage, globalPctPnLToday,
                    globalTodayIntradayValue, globalTodayDeliveryValue);

                var finalGlobalPnLLines = globalPnLLines.ToList();
                finalGlobalPnLLines.Add(globalSummaryToday);
                File.WriteAllLines(globalPnLFilePath, finalGlobalPnLLines);
            }
            catch(Exception ex)
            {
                Trace("Error:" + ex.Message + "\nStacktrace:" + ex.StackTrace);
            }
        }

        public static bool IsHolidayToday()
        {
            // Read up holidays list
            var filesPath = SystemUtils.GetStockFilesPath();
            string holidaysFilePath = Path.Combine(filesPath, "holidayslist.txt");
            var holidayDateStrs = File.ReadAllLines(holidaysFilePath);

            var holidayDates = holidayDateStrs.Select(h => DateTime.Parse(h));

            return holidayDates.Any(hd => hd.Date == DateTime.Today.Date);
        }

        public static List<UpstoxTradeParams> ReadTradingConfigFile()
        {
            var filesPath = SystemUtils.GetStockFilesPath();
            string configFilePath = Path.Combine(filesPath, "stocktradingconfig.txt");
            int Index = -1;
            var ctp = new UpstoxTradeParams();

            double mktConditionBuyExtraMarkDown = 0;
            double markDownPctForAveragingTightening = 0;
            double priceBucketWidthInPctForQty = 0;
            double[] priceBucketsForQty = new[]{ 0.0};
            double qtyAgressionFactor = 0;
            double[] priceBucketsForPrice = new[] { 0.0 };
            double deliveryBrokerage = 0;

            var lines = File.ReadAllLines(configFilePath);
 
            // Common config
            foreach (var line in lines.Where(l => !string.IsNullOrEmpty(l.Trim()) && l.Trim().StartsWith("@")))
            {
                var split = line.Split('=');

                switch (split[0].Trim())
                {
                    case "@mktConditionBuyExtraMarkDownPct":
                        mktConditionBuyExtraMarkDown = double.Parse(split[1])/100;
                        break;

                    case "@markDownPctForAveragingTightening":
                        markDownPctForAveragingTightening = double.Parse(split[1]);
                        break;

                    case "@priceBucketWidthInPctForQty":
                        priceBucketWidthInPctForQty = double.Parse(split[1])/100;
                        break;

                    case "@qtyAgressionFactor":
                        qtyAgressionFactor = double.Parse(split[1]);
                        break;

                    case "@priceBucketsForQty":
                        priceBucketsForQty = split[1].Split(',').Select(a => double.Parse(a)).ToArray();
                        break;

                    case "@priceBucketsForPrice":
                        priceBucketsForPrice = split[1].Split(',').Select(a => double.Parse(a)).ToArray();
                        break;

                    case "@deliveryBrokerage":
                        deliveryBrokerage = double.Parse(split[1])/100;
                        break;

                    case "@commonStock":
                        var common = split[1].Split(',');
                        Index = -1;
                        ctp.stockCode = "COMMONCONFIG";
                        ctp.baseOrderVal = double.Parse(common[++Index]);
                        ctp.maxTotalPositionValueMultiple = int.Parse(common[++Index]);
                        ctp.maxTodayPositionValueMultiple = int.Parse(common[++Index]);
                        ctp.markDownPctForBuy = double.Parse(common[++Index])/100;
                        ctp.markDownPctForAveraging = double.Parse(common[++Index])/100;
                        ctp.sellMarkup = double.Parse(common[++Index]);
                        ctp.placeBuyNoLtpCompare = bool.Parse(common[++Index]);
                        ctp.startTime = GeneralUtils.GetTodayDateTime(common[++Index]);
                        ctp.endTime = GeneralUtils.GetTodayDateTime(common[++Index]);
                        ctp.orderType = (EquityOrderType)Enum.Parse(typeof(EquityOrderType), common[++Index]);
                        ctp.exchange = (Exchange)Enum.Parse(typeof(Exchange), common[++Index]);
                        break;
                }

            }

            // Stocks config
            List<UpstoxTradeParams> tps = new List<UpstoxTradeParams>(lines.Length);
            foreach (var line in lines.Where(l => !string.IsNullOrEmpty(l.Trim()) && !(l.Trim().StartsWith("#") || l.Trim().StartsWith("@"))))
            {
                Index = -1;
                var stock = line.Split(',');

                var tp = new UpstoxTradeParams
                {
                    stockCode = stock[++Index],
                    baseOrderVal = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.baseOrderVal : double.Parse(stock[Index])) : ctp.baseOrderVal,
                    maxTotalPositionValueMultiple = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.maxTotalPositionValueMultiple : int.Parse(stock[Index])) : ctp.maxTotalPositionValueMultiple,
                    maxTodayPositionValueMultiple = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.maxTodayPositionValueMultiple : int.Parse(stock[Index])) : ctp.maxTodayPositionValueMultiple,
                    markDownPctForBuy = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.markDownPctForBuy : double.Parse(stock[Index]))/100 : ctp.markDownPctForBuy,//7
                    markDownPctForAveraging = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.markDownPctForAveraging : double.Parse(stock[Index]))/100 : ctp.markDownPctForAveraging,//6
                    sellMarkup = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.sellMarkup : double.Parse(stock[Index])) : ctp.sellMarkup,//8
                    placeBuyNoLtpCompare = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.placeBuyNoLtpCompare : bool.Parse(stock[Index])) : ctp.placeBuyNoLtpCompare,//12
                    startTime = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.startTime : GeneralUtils.GetTodayDateTime(stock[Index])) : ctp.startTime,//15
                    endTime = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.endTime : GeneralUtils.GetTodayDateTime(stock[Index])) : ctp.endTime,//16
                    orderType = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.orderType : (EquityOrderType)Enum.Parse(typeof(EquityOrderType), stock[Index])) : ctp.orderType,
                    exchange = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.exchange : (Exchange)Enum.Parse(typeof(Exchange), stock[Index])) : ctp.exchange
                };

                tp.markDownPctForAveragingTightening = markDownPctForAveragingTightening;
                tp.mktConditionBuyExtraMarkDownPct = mktConditionBuyExtraMarkDown;
                tp.deliveryBrokerage = deliveryBrokerage;
                tp.priceBucketWidthForQty = priceBucketWidthInPctForQty;
                tp.priceBucketsForQty = priceBucketsForQty;
                tp.qtyAgressionFactor = qtyAgressionFactor;
                tp.priceBucketsForPrice = priceBucketsForPrice;

                tps.Add(tp);
            }

            return tps;
        }

        public static void Trace(string inMessage)
        {
            var consoleMessage = DateTime.Now.ToString() + " " + inMessage;
            Console.WriteLine(consoleMessage);
            FileTracing.TraceOut(inMessage);
        }

        public static string GetLogContent()
        {
            FileStream logFileStream = new FileStream(FileTracing.GetTraceFilename(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            StreamReader logFileReader = new StreamReader(logFileStream);
            string logContent = logFileReader.ReadToEnd();

            logFileReader.Close();
            logFileStream.Close();

            return logContent;
        }
    }

    public class UpstoxPnLStats
    {
        public int prevHoldingQty = 0;
        public double prevHoldingPrice = 0;
        public double maxBuyValueToday = 0;
        public double baseOrderQty;
    }

    public class UpstoxMarketTrendParams
    {
        public MyUpstoxWrapper upstox;
        public string stockCode;
        public string exchangeStr;
    }

    public class UpstoxTradeParams
    {
        public MyUpstoxWrapper upstox;
        public UpstoxPnLStats stats = new UpstoxPnLStats();
        public MarketTrend mktTrend;

        // common algo config
        public double mktConditionBuyExtraMarkDownPct;
        public double markDownPctForAveragingTightening;
        public double priceBucketWidthForQty;
        public double[] priceBucketsForQty;
        public double qtyAgressionFactor;
        public double[] priceBucketsForPrice;

        public string stockCode;
        public double baseOrderVal = 50000;
        public int maxTotalPositionValueMultiple;
        public int maxTodayPositionValueMultiple;
        public Exchange exchange;

        // default
        public double markDownPctForBuy = 0.01;
        public double markDownPctForAveraging = 0.01;
        public double sellMarkup = 1.02;
        public double deliveryBrokerage = .0035;
        public bool placeBuyNoLtpCompare = false;
        public EquityOrderType orderType = EquityOrderType.MARGIN;

        public DateTime startTime = MarketUtils.GetTimeToday(9, 0);
        public DateTime endTime = MarketUtils.GetTimeToday(15, 0);
    }

    public class AlgoUtils
    {
        public static string GetPositionFile(string stockCode)
        {
            return Path.Combine(SystemUtils.GetPositionFilesLocation(), @"PositionFile_" + stockCode + ".txt");
        }

    

        //public static BrokerErrorCode GetLTPOnDemand(string exchStr, string stockCode, out double ltp)
        //{
        //    EquitySymbolQuote[] quote;
        //    BrokerErrorCode errCode = BrokerErrorCode.Unknown;
        //    int retryCount = 0;
        //    DateTime lut;
        //    ltp = 0.0;

        //    while (errCode != BrokerErrorCode.Success && retryCount++ < 3)
        //        try
        //        {
        //            errCode = myUpstoxWrapper.GetEquityLTP(exchStr, stockCode, out ltp, out lut);
        //            if (MarketUtils.IsMarketOpen())
        //            {
        //                if (DateTime.Now - lut >= TimeSpan.FromMinutes(5))
        //                    ltp = 0;
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            Trace("Error:" + ex.Message + "\nStacktrace:" + ex.StackTrace);
        //            if (retryCount >= 3)
        //                break;
        //        }

        //    return errCode;
        //}
    }
}
