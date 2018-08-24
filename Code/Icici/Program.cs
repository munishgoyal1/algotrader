using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using StockTrader.Core;
using StockTrader.Platform.Logging;
using StockTrader.Utilities;
using StockTrader.Utilities.Broker;

namespace SimpleTrader
{
    class Program
    {
        static string userId = "meghagoyal1";
        //static string userId = "MUNISGcS";

        static Mutex mutex = new Mutex(false, userId);

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

        static void Main(string[] args)
        {
            BrokerErrorCode errCode = BrokerErrorCode.Unknown;

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
            MarketUtils.WaitUntilMarketOpen();

            // Broker
            var broker = new IciciDirectBroker(userId, "AMIT1978", "08071986");
            //var broker = new IciciDirectBroker(userId, "amit1978", "07111982");
            broker.LogIn();

            //// Separate Login thread in background
            //BrokingAccountThread loginThread = new BrokingAccountThread();
            //Thread thread = new Thread(new ParameterizedThreadStart(LoginUtils.Background_LoginCheckerThread));
            //BrokingAccountObject iciciAccObj = new BrokingAccountObject(broker);
            //loginThread.brokerAccountObj = iciciAccObj;
            //loginThread.thread = thread;
            //loginThread.thread.Name = "Main Login Thread of MUNISGcS";
            //loginThread.thread.IsBackground = true;
            //loginThread.thread.Start(iciciAccObj);

            // ************ TESTING Code starts ****************** //
            //Trace("Testing starts");
            //string ordertestref = "";
            //double funds;
            //errCode = broker.GetFundsAvailable(out funds);
            //errCode = broker.AllocateFunds(FundAllocationCategory.IpoMf, funds);
            //errCode = broker.AllocateFunds(FundAllocationCategory.IpoMf, -4.67);
            //var str = string.Format("[Holding EOD] {0} {1}", OrderPositionTypeEnum.Btst, "");
            //errCode = CancelEquityOrder(string.Format("[Holding EOD] {0} {1}", HoldingTypeEnum.Btst, ""), ref ordertestref, OrderDirection.SELL);
            //var newTrades = new Dictionary<string, EquityTradeBookRecord>();
            //List<EquityDematHoldingRecord> holdings;
            //List<EquityBTSTTradeBookRecord> btstHoldings;
            //errCode = broker.GetBTSTListings("CAPFIR", out btstHoldings);
            //List<EquityPendingPositionForDelivery> pendingPositions;
            //errCode = broker.GetOpenPositionsPendingForDelivery("BAJFI", out pendingPositions);
            //errCode = broker.GetOpenPositionsPendingForDelivery("MOTSUM", out pendingPositions);
            //Dictionary<string, EquityOrderBookRecord> orders;
            //errCode = broker.GetEquityOrderBookToday(false, false, "MOTSUM", out orders);
            //errCode = broker.PlaceEquityMarginSquareOffOrder("BAJFI", 34, 34, "1733", OrderPriceType.LIMIT, OrderDirection.SELL, EquityOrderType.DELIVERY, "2017232", Exchange.NSE, out ordertestref);
            //errCode = broker.PlaceEquityMarginDeliveryFBSOrder("BAJFI", 5, "1733", OrderPriceType.LIMIT, OrderDirection.SELL, EquityOrderType.DELIVERY, Exchange.NSE, out ordertestref);

            //errCode = broker.GetDematAllocation("BAJFI", out holdings);
            //errCode = broker.GetDematAllocation("CAPFIR", out holdings);
            //errCode = broker.GetDematAllocation("CAPPOI", out holdings);
            //errCode = broker.ConvertToDeliveryFromMarginOpenPositions("BAJFI", 3, 1, "2017223", OrderDirection.BUY, Exchange.NSE);
            //errCode = broker.GetEquityTradeBookToday(true, "BAJFI", out newTrades);
            //errCode = broker.ConvertToDeliveryFromPendingForDelivery("CAPFIR", 4, 1, "2017217", Exchange.NSE);
            //errCode = broker.CancelEquityOrder("20171120N900017136", EquityOrderType.DELIVERY);
            //while (errCode != BrokerErrorCode.Success)
            //{
            //    errCode = broker.CancelAllOutstandingEquityOrders("BAJFI", out Dictionary<string, BrokerErrorCode> cancelledOrders);
            //    errCode = broker.PlaceEquityDeliveryBTSTOrder("BAJFI", 1, "1831", OrderPriceType.LIMIT, OrderDirection.SELL, EquityOrderType.DELIVERY, Exchange.NSE, "2017221", out ordertestref);
            //}
            ////errCode = broker.PlaceEquityMarginDeliveryFBSOrder("BAJFI", 1, "1831", OrderPriceType.LIMIT, OrderDirection.SELL, EquityOrderType.DELIVERY, Exchange.NSE, out ordertestref);
            //Trace("Testing ends");
            // ************ TESTING Code ends ****************** //

            // Ensure all funds are allocated
            double funds;
            errCode = BrokerErrorCode.Unknown;
            errCode = broker.GetFundsAvailable(out funds);
            funds -= 1000;
            funds = funds < 0 ? 0 : funds;
            if (funds > 0)
            {
                errCode = broker.AllocateFunds(FundAllocationCategory.Equity, funds);
                Trace(string.Format("Ensure free funds [{0}] are allocated to Equity: {1}", funds, errCode));
            }

            // Read the config file
            List <TradeParams> stocksConfig = ReadTradingConfigFile();

            var threads = new List<Thread>(stocksConfig.Count);

            foreach (var stockConfig in stocksConfig)
            {
                stockConfig.broker = broker;
                var t = new Thread(new AverageTheBuyThenSell(stockConfig).StockBuySell);
                threads.Add(t);
            }

            threads.ForEach(t => { t.Start(); Thread.Sleep(1000); });
            threads.ForEach(t => t.Join());

            // Send out the log in email and chat
            Trace("All stock threads completed. Emailing today's log file");
            MessagingUtils.Init();
            var log = GetLogContent();
            MessagingUtils.SendAlertMessage("SimpleTrader log", log);
            Trace("Exiting..");
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

        public static List<TradeParams> ReadTradingConfigFile()
        {
            var filesPath = SystemUtils.GetStockFilesPath();

            string configFilePath = Path.Combine(filesPath, "stocktradingconfig.txt");

            var lines = File.ReadAllLines(configFilePath);
            var common = lines[1].Split(',');

            List<TradeParams> tps = new List<TradeParams>(lines.Length);

            var ctp = new TradeParams
            {
                stockCode = "COMMONCONFIG",
                isinCode = "",
                maxTradeValue = double.Parse(common[0]),
                maxTotalPositionValueMultiple = int.Parse(common[1]),
                maxTodayPositionValueMultiple = int.Parse(common[2]),
                pctExtraMarkdownForAveraging = double.Parse(common[3]),
                buyMarkdownFromLcpDefault = double.Parse(common[4]),
                sellMarkupForMargin = double.Parse(common[5]),
                sellMarkupForDelivery = double.Parse(common[6]),
                sellMarkupForMinProfit = double.Parse(common[7]),
                pctSquareOffForMinProfit = double.Parse(common[8]),
                squareOffAllPositionsAtEOD = bool.Parse(common[9]),
                pctMaxLossSquareOffPositionsAtEOD = double.Parse(common[10]),
                useAvgBuyPriceInsteadOfLastBuyPriceToCalculateBuyPriceForNewOrder = bool.Parse(common[11]),
                startTime = GeneralUtils.GetTodayDateTime(common[12]),
                endTime = GeneralUtils.GetTodayDateTime(common[13]),
                exchange = (Exchange)Enum.Parse(typeof(Exchange), common[14]),
                sellMarkupForEODInsufficientLimitSquareOff = double.Parse(common[15]),
                maxBuyOrdersAllowedInADay = int.Parse(common[16]),
                doConvertToDeliveryAtEOD = bool.Parse(common[17]),
                doSquareOffIfInsufficientLimitAtEOD = bool.Parse(common[18])
            };

            for (int i = 2; i < lines.Length; i++)
            {
                var stock = lines[i].Split(',');
                var stockCode = stock[0];
                if (stockCode.Trim().StartsWith("#"))
                    continue;

                var goodPrice = double.Parse(stock[1]);
                var buyPriceCap = double.Parse(stock[2]);

                var maxTradeVal = stock.Length > 3 ? (string.IsNullOrEmpty(stock[3]) ? ctp.maxTradeValue : double.Parse(stock[3])) : ctp.maxTradeValue;
                var ordQty = (int)Math.Round(maxTradeVal / goodPrice);
                var maxTotalPositionValueMultiple = stock.Length > 4 ? (string.IsNullOrEmpty(stock[4]) ? ctp.maxTotalPositionValueMultiple : int.Parse(stock[4])) : ctp.maxTotalPositionValueMultiple;
                var maxTodayPositionValueMultiple = stock.Length > 5 ? (string.IsNullOrEmpty(stock[5]) ? ctp.maxTodayPositionValueMultiple : int.Parse(stock[5])) : ctp.maxTodayPositionValueMultiple;

                var tp = new TradeParams
                {
                    stockCode = stockCode,
                    isinCode = "",
                    maxTradeValue = maxTradeVal,
                    maxTotalPositionValueMultiple = maxTotalPositionValueMultiple,
                    maxTodayPositionValueMultiple = maxTodayPositionValueMultiple,
                    ordQty = ordQty,
                    maxTotalOutstandingQtyAllowed = ordQty * maxTotalPositionValueMultiple,
                    maxTodayOutstandingQtyAllowed = ordQty * maxTodayPositionValueMultiple,
                    goodPrice = goodPrice,
                    buyPriceCap = buyPriceCap,
                    pctExtraMarkdownForAveraging = stock.Length > 6 ? (string.IsNullOrEmpty(stock[6]) ? ctp.pctExtraMarkdownForAveraging : double.Parse(stock[6])) : ctp.pctExtraMarkdownForAveraging,
                    buyMarkdownFromLcpDefault = stock.Length > 7 ? (string.IsNullOrEmpty(stock[7]) ? ctp.buyMarkdownFromLcpDefault : double.Parse(stock[7])) : ctp.buyMarkdownFromLcpDefault,
                    sellMarkupForMargin = stock.Length > 8 ? (string.IsNullOrEmpty(stock[8]) ? ctp.sellMarkupForMargin : double.Parse(stock[8])) : ctp.sellMarkupForMargin,
                    sellMarkupForDelivery = stock.Length > 9 ? (string.IsNullOrEmpty(stock[9]) ? ctp.sellMarkupForDelivery : double.Parse(stock[9])) : ctp.sellMarkupForDelivery,
                    sellMarkupForMinProfit = stock.Length > 10 ? (string.IsNullOrEmpty(stock[10]) ? ctp.sellMarkupForMinProfit : double.Parse(stock[10])) : ctp.sellMarkupForMinProfit,
                    pctSquareOffForMinProfit = stock.Length > 11 ? (string.IsNullOrEmpty(stock[11]) ? ctp.pctSquareOffForMinProfit : double.Parse(stock[11])) : ctp.pctSquareOffForMinProfit,
                    squareOffAllPositionsAtEOD = stock.Length > 12 ? (string.IsNullOrEmpty(stock[12]) ? ctp.squareOffAllPositionsAtEOD : bool.Parse(stock[12])) : ctp.squareOffAllPositionsAtEOD,
                    pctMaxLossSquareOffPositionsAtEOD = stock.Length > 13 ? (string.IsNullOrEmpty(stock[13]) ? ctp.pctMaxLossSquareOffPositionsAtEOD : double.Parse(stock[13])) : ctp.pctMaxLossSquareOffPositionsAtEOD,
                    useAvgBuyPriceInsteadOfLastBuyPriceToCalculateBuyPriceForNewOrder = stock.Length > 14 ? (string.IsNullOrEmpty(stock[14]) ? ctp.useAvgBuyPriceInsteadOfLastBuyPriceToCalculateBuyPriceForNewOrder : bool.Parse(stock[14])) : ctp.useAvgBuyPriceInsteadOfLastBuyPriceToCalculateBuyPriceForNewOrder,
                    startTime = stock.Length > 15 ? (string.IsNullOrEmpty(stock[15]) ? ctp.startTime : GeneralUtils.GetTodayDateTime(stock[15])) : ctp.startTime,
                    endTime = stock.Length > 16 ? (string.IsNullOrEmpty(stock[16]) ? ctp.endTime : GeneralUtils.GetTodayDateTime(stock[16])) : ctp.endTime,
                    exchange = stock.Length > 17 ? (string.IsNullOrEmpty(stock[17]) ? ctp.exchange : (Exchange)Enum.Parse(typeof(Exchange), stock[17])) : ctp.exchange,
                    sellMarkupForEODInsufficientLimitSquareOff = stock.Length > 18 ? (string.IsNullOrEmpty(stock[18]) ? ctp.sellMarkupForEODInsufficientLimitSquareOff : double.Parse(stock[18])) : ctp.sellMarkupForEODInsufficientLimitSquareOff,
                    maxBuyOrdersAllowedInADay = stock.Length > 19 ? (string.IsNullOrEmpty(stock[19]) ? ctp.maxBuyOrdersAllowedInADay : int.Parse(stock[19])) : ctp.maxBuyOrdersAllowedInADay,
                    doConvertToDeliveryAtEOD = stock.Length > 20 ? (string.IsNullOrEmpty(stock[20]) ? ctp.doConvertToDeliveryAtEOD : bool.Parse(stock[20])) : ctp.doConvertToDeliveryAtEOD,
                    doSquareOffIfInsufficientLimitAtEOD = stock.Length > 21 ? (string.IsNullOrEmpty(stock[21]) ? ctp.doSquareOffIfInsufficientLimitAtEOD : bool.Parse(stock[21])) : ctp.doSquareOffIfInsufficientLimitAtEOD
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

    public class TradeParams
    {
        public IBroker broker;
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
        public double pctExtraMarkdownForAveraging = 0.99;
        public double buyMarkdownFromLcpDefault = 0.99;
        public double sellMarkupForMargin = 1.02;
        public double sellMarkupForDelivery = 1.025;
        public double sellMarkupForMinProfit = 1.0035;
        public double sellMarkupForEODInsufficientLimitSquareOff = 0.995;
        public double pctSquareOffForMinProfit = 0.0035;
        public bool squareOffAllPositionsAtEOD = false;
        public double pctMaxLossSquareOffPositionsAtEOD = .01;
        public bool useAvgBuyPriceInsteadOfLastBuyPriceToCalculateBuyPriceForNewOrder = false;
        public bool doConvertToDeliveryAtEOD = true;
        public bool doSquareOffIfInsufficientLimitAtEOD = false;

        public DateTime startTime = MarketUtils.GetTimeToday(9, 0);
        public DateTime endTime = MarketUtils.GetTimeToday(15, 0);
    }
}
