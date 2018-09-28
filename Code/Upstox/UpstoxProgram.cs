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
        static double _pctLtpOfLastBuyPriceForAveraging = 0.99;
        static double _buyMarkdownFromLtpDefault = 0.985;
        static double _sellMarkupForMargin = 1.015;
        static double _sellMarkupForDelivery = 1.025;   // 2.5%
        static double _sellMarkupForMinProfit = 1.0035; // .35%
        static double _sellMarkupForEODInsufficientLimitSquareOff = 0.995;
        static double _pctSquareOffForMinProfit = 0.0035;
        static bool _squareOffAllPositionsAtEOD = false;
        static double _pctMaxLossSquareOffPositionsAtEOD = .002; // 0.5%
        static bool _useAvgBuyPriceInsteadOfLastBuyPriceToCalculateBuyPriceForNewOrder = false;
        static bool _doConvertToDeliveryAtEOD = true;
        static bool _doSquareOffIfInsufficientLimitAtEOD = false;
        static DateTime _startTime = MarketUtils.GetTimeToday(9, 0);
        static DateTime _endTime = MarketUtils.GetTimeToday(15, 00);
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
            Trace("DEBUG MODE"); errCode = upstoxBroker.Login1();
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
                //Console.WriteLine("Press any key to exit..");
                //Console.ReadKey();
                return;
            }

            Trace(userId);

#if !DEBUG
            MarketUtils.WaitUntilMarketOpen();
#endif

            //while (true)
            //     Thread.Sleep(1000);

            //// Separate Login thread in background
            //BrokingAccountThread loginThread = new BrokingAccountThread();
            //Thread thread = new Thread(new ParameterizedThreadStart(LoginUtils.Background_LoginCheckerThread));
            //BrokingAccountObject iciciAccObj = new BrokingAccountObject(upstox);
            //loginThread.brokerAccountObj = iciciAccObj;

            //loginThread.thread = thread;
            //loginThread.thread.Name = "Main Login Thread of MUNISGcS";
            //loginThread.thread.IsBackground = true;
            //loginThread.thread.Start(iciciAccObj);

            // ************ TESTING Code starts ****************** //
            //Trace("Testing starts");
            //string ordertestref = "";aswwww345678899990
            //double funds;
            //errCode = upstox.GetFundsAvailable(out funds);
            //errCode = upstox.AllocateFunds(FundAllocationCategory.IpoMf, funds);
            //errCode = upstox.AllocateFunds(FundAllocationCategory.IpoMf, -4.67);
            //var str = string.Format("[Holding EOD] {0} {1}", OrderPositionTypeEnum.Btst, "");
            //errCode = CancelEquityOrder(string.Format("[Holding EOD] {0} {1}", HoldingTypeEnum.Btst, ""), ref ordertestref, OrderDirection.SELL);
            //var newTrades = new Dictionary<string, EquityTradeBookRecord>();
            //List<EquityDematHoldingRecord> holdings;
            //List<EquityBTSTTradeBookRecord> btstHoldings;
            //errCode = upstox.GetBTSTListings("CAPFIR", out btstHoldings);
            //List<EquityPendingPositionForDelivery> pendingPositions;
            //errCode = upstox.GetOpenPositionsPendingForDelivery("BAJFI", out pendingPositions);
            //errCode = upstox.GetOpenPositionsPendingForDelivery("MOTSUM", out pendingPositions);
            //Dictionary<string, EquityOrderBookRecord> orders;
            //errCode = upstox.GetEquityOrderBookToday(false, false, "MOTSUM", out orders);
            //errCode = upstox.PlaceEquityMarginSquareOffOrder("BAJFI", 34, 34, "1733", OrderPriceType.LIMIT, OrderDirection.SELL, EquityOrderType.DELIVERY, "2017232", Exchange.NSE, out ordertestref);
            //errCode = upstox.PlaceEquityMarginDeliveryFBSOrder("BAJFI", 5, "1733", OrderPriceType.LIMIT, OrderDirection.SELL, EquityOrderType.DELIVERY, Exchange.NSE, out ordertestref);

            //errCode = upstox.GetDematAllocation("BAJFI", out holdings);
            //errCode = upstox.GetDematAllocation("CAPFIR", out holdings);
            //errCode = upstox.GetDematAllocation("CAPPOI", out holdings);
            //errCode = upstox.ConvertToDeliveryFromMarginOpenPositions("BAJFI", 3, 1, "2017223", OrderDirection.BUY, Exchange.NSE);
            //errCode = upstox.GetEquityTradeBookToday(true, "BAJFI", out newTrades);
            //errCode = upstox.ConvertToDeliveryFromPendingForDelivery("CAPFIR", 4, 1, "2017217", Exchange.NSE);
            //errCode = upstox.CancelEquityOrder("20171120N900017136", EquityOrderType.DELIVERY);
            //while (errCode != BrokerErrorCode.Success)
            //{
            //    errCode = upstox.CancelAllOutstandingEquityOrders("BAJFI", out Dictionary<string, BrokerErrorCode> cancelledOrders);
            //    errCode = upstox.PlaceEquityDeliveryBTSTOrder("BAJFI", 1, "1831", OrderPriceType.LIMIT, OrderDirection.SELL, EquityOrderType.DELIVERY, Exchange.NSE, "2017221", out ordertestref);
            //}
            ////errCode = upstox.PlaceEquityMarginDeliveryFBSOrder("BAJFI", 1, "1831", OrderPriceType.LIMIT, OrderDirection.SELL, EquityOrderType.DELIVERY, Exchange.NSE, out ordertestref);
            //Trace("Testing ends");
            // ************ TESTING Code ends ****************** //

            Thread.Sleep(5000);// Let the rates etc update on server

            // Read the config file
            List<UpstoxTradeParams> stocksConfig = ReadTradingConfigFile();

            var threads = new List<Thread>(stocksConfig.Count);

            foreach (var stockConfig in stocksConfig)
            {
                stockConfig.upstox = upstoxBroker;
                upstoxBroker.AddStock(stockConfig.stockCode);
                var t = new Thread(new UpstoxAverageTheBuyThenSell(stockConfig).StockBuySell);
                threads.Add(t);
            }

            threads.ForEach(t => { t.Start(); Thread.Sleep(200); });
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
            // calculate stats
            // File.WriteAllLines(positionFile, lines);

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

                var configToday = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}", stockConfig.stockCode, stockConfig.goodPrice,
                        stockConfig.buyPriceCap, stockConfig.buyMarkdownFromLcpDefault,
                        stockConfig.sellMarkupForMargin, stockConfig.sellMarkupForDelivery, stockConfig.pctExtraMarkdownForAveraging,
                        stockConfig.placeBuyNoLtpCompare, stockConfig.startTime.ToString("hh:mm"));

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

                var orderQty = stockConfig.ordQty;

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
                    todayBuyTrades, todaySellTrades, orderQty, todayBuyQty, todaySellQty, todayIntradayValue, todayDeliveryValue);

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

            var lines = File.ReadAllLines(configFilePath);
            var common = lines[1].Split(',');

            List<UpstoxTradeParams> tps = new List<UpstoxTradeParams>(lines.Length);

            int Index = -1;
            var ctp = new UpstoxTradeParams
            {
                stockCode = "COMMONCONFIG",
                isinCode = "",
                orderType = (EquityOrderType)Enum.Parse(typeof(EquityOrderType), common[++Index]),
                maxTradeValue = double.Parse(common[++Index]),
                maxTotalPositionValueMultiple = int.Parse(common[++Index]),
                maxTodayPositionValueMultiple = int.Parse(common[++Index]),
                buyMarkdownFromLcpDefault = double.Parse(common[++Index]),
                sellMarkupForMargin = double.Parse(common[++Index]),
                sellMarkupForDelivery = double.Parse(common[++Index]),
                pctExtraMarkdownForAveraging = double.Parse(common[++Index]),
                sellMarkupForMinProfit = double.Parse(common[++Index]),
                placeBuyNoLtpCompare = bool.Parse(common[++Index]),
                squareOffAllPositionsAtEOD = bool.Parse(common[++Index]),
                pctMaxLossSquareOffPositionsAtEOD = double.Parse(common[++Index]),
                useAvgBuyPriceInsteadOfLastBuyPriceToCalculateBuyPriceForNewOrder = bool.Parse(common[++Index]),
                startTime = GeneralUtils.GetTodayDateTime(common[++Index]),
                endTime = GeneralUtils.GetTodayDateTime(common[++Index]),
                exchange = (Exchange)Enum.Parse(typeof(Exchange), common[++Index]),
                sellMarkupForEODInsufficientLimitSquareOff = double.Parse(common[++Index]),
                maxBuyOrdersAllowedInADay = int.Parse(common[++Index]),
                doConvertToDeliveryAtEOD = bool.Parse(common[++Index]),
                doSquareOffIfInsufficientLimitAtEOD = bool.Parse(common[++Index])
            };

            for (int i = 2; i < lines.Length; i++)
            {
                Index = -1;
                var stock = lines[i].Split(',');
                var stockCode = stock[++Index];//0
                if (stockCode.Trim().StartsWith("#"))
                    continue;

                var goodPrice = double.Parse(stock[++Index]);//1
                var buyPriceCap = double.Parse(stock[++Index]);//2
                var orderType = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.orderType : (EquityOrderType)Enum.Parse(typeof(EquityOrderType), stock[Index])) : ctp.orderType;//22
                var maxTradeVal = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.maxTradeValue : double.Parse(stock[Index])) : ctp.maxTradeValue;//3
                var ordQty = (int)Math.Round(maxTradeVal / goodPrice);
                var maxTotalPositionValueMultiple = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.maxTotalPositionValueMultiple : int.Parse(stock[Index])) : ctp.maxTotalPositionValueMultiple;//4
                var maxTodayPositionValueMultiple = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.maxTodayPositionValueMultiple : int.Parse(stock[Index])) : ctp.maxTodayPositionValueMultiple;//5

                var tp = new UpstoxTradeParams
                {
                    stockCode = stockCode,
                    isinCode = "",
                    maxTradeValue = maxTradeVal,
                    maxTotalPositionValueMultiple = maxTotalPositionValueMultiple,
                    maxTodayPositionValueMultiple = maxTodayPositionValueMultiple,
                    orderType = orderType,
                    ordQty = ordQty,
                    maxTotalOutstandingQtyAllowed = ordQty * maxTotalPositionValueMultiple,
                    maxTodayOutstandingQtyAllowed = ordQty * maxTodayPositionValueMultiple,
                    goodPrice = goodPrice,
                    buyPriceCap = buyPriceCap,
                    buyMarkdownFromLcpDefault = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.buyMarkdownFromLcpDefault : double.Parse(stock[Index])) : ctp.buyMarkdownFromLcpDefault,//7
                    sellMarkupForMargin = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.sellMarkupForMargin : double.Parse(stock[Index])) : ctp.sellMarkupForMargin,//8
                    sellMarkupForDelivery = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.sellMarkupForDelivery : double.Parse(stock[Index])) : ctp.sellMarkupForDelivery,//9
                    pctExtraMarkdownForAveraging = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.pctExtraMarkdownForAveraging : double.Parse(stock[Index])) : ctp.pctExtraMarkdownForAveraging,//6
                    sellMarkupForMinProfit = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.sellMarkupForMinProfit : double.Parse(stock[Index])) : ctp.sellMarkupForMinProfit,//10
                    placeBuyNoLtpCompare = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.placeBuyNoLtpCompare : bool.Parse(stock[Index])) : ctp.placeBuyNoLtpCompare,//12
                    squareOffAllPositionsAtEOD = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.squareOffAllPositionsAtEOD : bool.Parse(stock[Index])) : ctp.squareOffAllPositionsAtEOD,//12
                    pctMaxLossSquareOffPositionsAtEOD = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.pctMaxLossSquareOffPositionsAtEOD : double.Parse(stock[Index])) : ctp.pctMaxLossSquareOffPositionsAtEOD,//13
                    useAvgBuyPriceInsteadOfLastBuyPriceToCalculateBuyPriceForNewOrder = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.useAvgBuyPriceInsteadOfLastBuyPriceToCalculateBuyPriceForNewOrder : bool.Parse(stock[Index])) : ctp.useAvgBuyPriceInsteadOfLastBuyPriceToCalculateBuyPriceForNewOrder,//14
                    startTime = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.startTime : GeneralUtils.GetTodayDateTime(stock[Index])) : ctp.startTime,//15
                    endTime = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.endTime : GeneralUtils.GetTodayDateTime(stock[Index])) : ctp.endTime,//16
                    exchange = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.exchange : (Exchange)Enum.Parse(typeof(Exchange), stock[Index])) : ctp.exchange,//17
                    sellMarkupForEODInsufficientLimitSquareOff = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.sellMarkupForEODInsufficientLimitSquareOff : double.Parse(stock[Index])) : ctp.sellMarkupForEODInsufficientLimitSquareOff,//18
                    maxBuyOrdersAllowedInADay = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.maxBuyOrdersAllowedInADay : int.Parse(stock[Index])) : ctp.maxBuyOrdersAllowedInADay,//19
                    doConvertToDeliveryAtEOD = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.doConvertToDeliveryAtEOD : bool.Parse(stock[Index])) : ctp.doConvertToDeliveryAtEOD,//20
                    doSquareOffIfInsufficientLimitAtEOD = stock.Length > ++Index ? (string.IsNullOrEmpty(stock[Index]) ? ctp.doSquareOffIfInsufficientLimitAtEOD : bool.Parse(stock[Index])) : ctp.doSquareOffIfInsufficientLimitAtEOD//21
                };

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
    }

    public class UpstoxTradeParams
    {
        public MyUpstoxWrapper upstox;
        public UpstoxPnLStats stats = new UpstoxPnLStats();

        public string stockCode;
        public string isinCode;
        public double maxTradeValue = 50000;
        public int maxTotalPositionValueMultiple = 4;
        public int maxTodayPositionValueMultiple = 2;
        public int ordQty;
        public int maxTotalOutstandingQtyAllowed;
        public int maxTodayOutstandingQtyAllowed;
        public int maxBuyOrdersAllowedInADay = 4;
        public Exchange exchange;

        // default
        public double buyPriceCap;
        public double goodPrice;
        public double pctExtraMarkdownForAveraging = 0.01;
        public double buyMarkdownFromLcpDefault = 0.01;
        public double sellMarkupForMargin = 1.02;
        public double sellMarkupForDelivery = 1.025;
        public double sellMarkupForMinProfit = 1.0035;
        public double sellMarkupForEODInsufficientLimitSquareOff = 0.995;
        public bool placeBuyNoLtpCompare = false;
        public bool squareOffAllPositionsAtEOD = false;
        public double pctMaxLossSquareOffPositionsAtEOD = .01;
        public bool useAvgBuyPriceInsteadOfLastBuyPriceToCalculateBuyPriceForNewOrder = false;
        public bool doConvertToDeliveryAtEOD = true;
        public bool doSquareOffIfInsufficientLimitAtEOD = false;
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
