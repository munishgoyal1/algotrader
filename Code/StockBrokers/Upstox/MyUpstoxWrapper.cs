using HtmlParsingLibrary;
using HttpLibrary;
using StockTrader.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StockTrader.Platform.Logging;
using UpstoxNet;

namespace StockTrader.Brokers.UpstoxBroker
{
    public class MyUpstoxWrapper
    {
        Upstox upstox = new Upstox();
        public Upstox Upstox { get { return upstox; } }

        // Locks on global Order & Trade book objects
        object lockObjectEquity = new object();


        object lockSingleThreadedUpstoxCall = new object();

        private Dictionary<string, EquityTradeBookRecord> mEquityTradeBook = new Dictionary<string, EquityTradeBookRecord>();
        private Dictionary<string, EquityOrderBookRecord> mEquityOrderBook = new Dictionary<string, EquityOrderBookRecord>();

        //public Dictionary<string, UpstoxBuySellBase> stockAlgos = new Dictionary<string, UpstoxBuySellBase>();

        public MyUpstoxWrapper(string apiKey, string apiSecret, string redirectUrl)
        {
            upstox.Api_Key = apiKey;
            upstox.Api_Secret = apiSecret;
            upstox.Redirect_Url = redirectUrl;//"https://upstox.com";  // should be exactly same in API dashboard
            //https://api.upstox.com/index/dialog/authorize?apiKey={}&redirect_uri=https://upstox.com&response_type=code
        }

        public BrokerErrorCode Login()
        {
            var result = upstox.Login();
            upstox.GetAccessToken();
            upstox.GetMasterContract();

            while (!upstox.Symbol_Download_Status)
                Thread.Sleep(1000);

            return result ? BrokerErrorCode.Success : BrokerErrorCode.Unknown;
        }

        //public event QuotesReceivedEventEventHandler QuotesReceivedEvent;

        public void QuoteReceived(object sender, QuotesReceivedEventArgs args)
        {
            //if (stockAlgos.Contains(args.TrdSym))
            //{
            //    Interlocked.Exchange(algo.Ltp, args.LTP);
            //}


        }

        public BrokerErrorCode Login1()
        {
            var result = upstox.Login();
            //upstox.GetAccessToken("3b4f89238b618427088c3e4ce1abef4be60605e3");
            upstox.GetAccessToken();
            upstox.GetMasterContract();

            while (!upstox.Symbol_Download_Status)
                Thread.Sleep(1000);


            Upstox.QuotesReceivedEvent += new UpstoxNet.Upstox.QuotesReceivedEventEventHandler(QuoteReceived);

            var subs = upstox.SubscribeQuotes("NSE_EQ", "CAPLIPOINT");



            var ltp = upstox.GetSnapLtp("NSE_EQ", "CAPLIPOINT");

            double ltpt;
            var abc = GetEquityLTP("NSE_EQ", "CAPLIPOINT", out ltpt);


            string orderRef;

            List<EquityDematHoldingRecord> holdings;
            var errCode = GetHoldings("FEDERALBNK", out holdings);


            Dictionary<string, EquityTradeBookRecord> trades;
            var tradebook = GetTradeBook(false, "IDFCBANK", out trades);

            Dictionary<string, EquityOrderBookRecord> orders;
            var orderbook = GetOrderBook(true, true, "BAJFINANCE", out orders);
            var netQty = GetNetQty("NSE_EQ", "IDFCBANK");

            var soldQty = upstox.GetSoldQty("NSE_EQ", "IDFCBANK");
            var boughtQty = GetBoughtQty("NSE_EQ", "IDFCBANK");

            //var orderAmo = upstox.PlaceAmo("NSE_EQ", "IDFCBANK", "B", "L", 2, "I", 45);
            //var orderAmoD = upstox.PlaceAmo("NSE_EQ", "FEDERALBNK", "B", "L", 1, "D", 77.5);
            var orderSimpleD = upstox.PlaceSimpleOrder("NSE_EQ", "FEDERALBNK", "S", "L", 1, "D", 83.9);
            //var orderSimpleD = upstox.PlaceSimpleOrder("NSE_EQ", "IDFCBANK", "S", "L", 1, "D", 45);

            var orderGet = upstox.GetOrderBook();

            //var lastOrderId = upstox.GetLastOrderId("NSE_EQ", "BAJFINANCE", "D");
            //180822000000760
            //180822000000836

            //string ordDetail = upstox.GetOrderDetails("180822000000760");

            //string a = upstox.GetOrderIds("NSE_EQ", "FEDERALBNK", "D"); // Product = D or I

            //string cancelAmo = upstox.CancelSimpleOrder("180823000023457"); // Product = D or I
            //Cancellation sent for [180822000000760]
            // UpstoxNet.OrderIdNotFoundException: 'OrderId is not found'

            //var order = upstox.PlaceSimpleOrder("NSE_EQ", "BAJFINANCE", "B", "L", 1, "D", 2750);
            //180822000000852
            //string cancelAmo1 = upstox.CancelAmo("180822000000852"); // Product = D or I

            var positions = upstox.GetPositions();
          
            //var b = upstox.GetOrder("NSE_EQ", "BAJFINANCE", "D");


            return result ? BrokerErrorCode.Success : BrokerErrorCode.Unknown;
            /*
            var mCookieContainer = new CookieContainer();

            var UPSTOX_LOGIN_URL = "https://api.upstox.com/index/dialog/authorize?apiKey=1EENJJ7o6O4JG1pWDVVpYaPsarWozbZs9NPJnek2&redirect_uri=https://upstox.com&response_type=code";
            //https://api.upstox.com/index/dialog/authorize?apiKey=6wiVNUdhRw10R7FzCxNso6zvKFaUU4RjlYe2Ltc9&redirect_uri=https://upstox.com&response_type=code
            //code = 5e56183a1ad7902d9c97249079b28cce3f6b9715
            var UPSTOX_LOGON_REFERRER = "https://api.upstox.com";

            var UPSTOX_AUTHORIZE_URL = "https://api.upstox.com/index/dialog/authorize/decision";

            string authorizeResponse = HttpHelper.GetWebPageResponse(UPSTOX_LOGIN_URL, null,
                UPSTOX_LOGON_REFERRER,
                mCookieContainer);

            var transactionId = StringParser.GetStringBetween(authorizeResponse,
                  0,
                  "<input name=\"transaction_id\" type=\"hidden\" value=\"",
                  "\"",
                  null).Trim();

            string postData = "transaction_id=" + transactionId;

            string decisionResponse = HttpHelper.GetWebPageResponse(UPSTOX_AUTHORIZE_URL, postData,
                UPSTOX_LOGON_REFERRER,
                mCookieContainer);

            var a = decisionResponse + "aa";

            var accessToken = upstoxBroker.GetAccessToken();
            */


        }

        public void LogOut()
        {
            upstox.Logout();
        }

        public OrderStatus ParseOrderStatus(string statusStr)
        {
            OrderStatus ordStatus = OrderStatus.NOTFOUND;

            var statusStrLower = statusStr.ToLower();

            if (statusStrLower.Contains("open pending") || statusStrLower.Contains("received") || statusStrLower.Contains("open"))
            {
                ordStatus = OrderStatus.ORDERED;
            }
            if (statusStrLower.Contains("rejected"))
            {
                ordStatus = OrderStatus.REJECTED;
            }
            else if (statusStrLower.Contains("complete"))
            {
                ordStatus = OrderStatus.COMPLETED;
            }
            else if (statusStrLower.Contains("cancelled"))
            {
                ordStatus = OrderStatus.CANCELLED;
            }


            return ordStatus;
        }

        // Equity methods
        public BrokerErrorCode GetEquityLTP(string exchange, string stockCode, out double ltp)
        {
            lock (lockSingleThreadedUpstoxCall)
            {
                ltp = upstox.GetLtp(exchange, stockCode);
                return BrokerErrorCode.Success;
            }
        }

        public BrokerErrorCode PlaceEquityOrder(
            string exchange,
            string stockCode,
            OrderDirection orderDirection,
            OrderPriceType orderPriceType,
            int quantity,
            EquityOrderType orderType,
            double price,
            out string orderId)
        {
            lock (lockSingleThreadedUpstoxCall)
            {
                BrokerErrorCode errorCode = BrokerErrorCode.Unknown;
                orderId = "";

                try
                {
                    var transType = orderDirection == OrderDirection.BUY ? "B" : "S";
                    var ordType = orderPriceType == OrderPriceType.LIMIT ? "L" : "M";
                    var prodType = orderType == EquityOrderType.DELIVERY ? "D" : "I";
                    orderId = upstox.PlaceSimpleOrder(exchange, stockCode, transType, ordType, quantity, prodType, price);

                    errorCode = BrokerErrorCode.Success;
                }
                catch (Exception ex)
                {
                    errorCode = BrokerErrorCode.Unknown;
                }

                return errorCode;
            }
        }


        //EXCHANGE,SYMBOL,TOKEN,PRODUCT,COLLATERAL_TYPE ,CNC_USED_QUANTITY,QUANTITY,COLLATERAL_QTY,HAIRCUT,AVG_PRICE
        //NSE_EQ,BAJFINANCE,317,D,WC,0,605,0,25,1625.9508
        public BrokerErrorCode GetHoldings(string stockCode, out List<EquityDematHoldingRecord> holdings)
        {
            lock (lockSingleThreadedUpstoxCall)
            {
                BrokerErrorCode errorCode = BrokerErrorCode.Unknown;
                holdings = new List<EquityDematHoldingRecord>();
                try
                {
                    var response = upstox.GetHoldings();

                    string[] lines = response.Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.None);

                    for (int i = 1; i < lines.Length; i++)
                    {
                        var line = lines[i].Split(',');

                        if (line.Length < 10)
                            continue;

                        if (!string.IsNullOrEmpty(stockCode) &&
                            !line[1].Equals(stockCode, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var holding = new EquityDematHoldingRecord();
                        holding.BlockedQuantity = int.Parse(line[5]);
                        holding.Quantity = int.Parse(line[6]);
                        holding.AvailableQuantity = holding.Quantity - holding.BlockedQuantity;
                        holding.StockCode = line[1];
                        holding.Exchange = line[0];

                        holdings.Add(holding);
                    }

                    errorCode = BrokerErrorCode.Success;
                }
                catch (Exception ex)
                {
                    errorCode = BrokerErrorCode.Unknown;
                }

                return errorCode;
            }
        }

        //EXCHANGE,TOKEN,SYMBOL,PRODUCT,ORDER_TYPE,TRANSACTION_TYPE,TRADED_QUANTITY,EXCHANGE_ORDER_ID,ORDER_ID,EXCHANGE_TIME,TIME_IN_MICRO,TRADED_PRICE,TRADE_ID
        public BrokerErrorCode GetTradeBook(bool newTradesOnly, string stockCode, out Dictionary<string, EquityTradeBookRecord> trades)
        {
            lock (lockSingleThreadedUpstoxCall)
            {
                BrokerErrorCode errorCode = BrokerErrorCode.Unknown;
                trades = new Dictionary<string, EquityTradeBookRecord>();

                int retryCount = 0;
                int maxRetryCount = 3;

                while (errorCode != BrokerErrorCode.Success && retryCount++ < maxRetryCount)
                    try
                    {
                        var response = upstox.GetTradeBook();

                        string[] lines = response.Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.None);

                        for (int i = 1; i < lines.Length; i++)
                        {
                            var line = lines[i].Split(',');

                            if (line.Length < 13)
                                continue;

                            if (!string.IsNullOrEmpty(stockCode) &&
                                !line[2].Equals(stockCode, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            var trade = new EquityTradeBookRecord();
                            trade.OrderId = line[8];
                            trade.Direction = line[5] == "B" ? OrderDirection.BUY : OrderDirection.SELL;
                            trade.DateTime = DateTime.Parse(line[9]);
                            trade.Quantity = int.Parse(line[6]);
                            trade.NewQuantity = trade.Quantity;
                            trade.Price = double.Parse(line[11]);
                            trade.StockCode = line[2];
                            trade.EquityOrderType = line[3] == "D" ? EquityOrderType.DELIVERY : EquityOrderType.MARGIN;
                            trade.Exchange = line[0];

                            lock (lockObjectEquity)
                            {
                                // existing trade
                                if (mEquityTradeBook.ContainsKey(trade.OrderId))
                                {
                                    var prevTradeRecord = mEquityTradeBook[trade.OrderId];

                                    // for part exec, the NewQuantity gets updated with delta from previous
                                    trade.NewQuantity = trade.Quantity - prevTradeRecord.Quantity;

                                    if (newTradesOnly)
                                    {
                                        // add if execution status change along with prev and current execution qty 
                                        if (trade.NewQuantity > 0)
                                            trades.Add(trade.OrderId, trade);
                                    }
                                    else
                                    {
                                        trades.Add(trade.OrderId, trade);
                                    }
                                    // Update the trade
                                    // update required because PartExec may have become full exec
                                    mEquityTradeBook[trade.OrderId] = trade;
                                }
                                else
                                {
                                    // new trade
                                    mEquityTradeBook.Add(trade.OrderId, trade);
                                    trades.Add(trade.OrderId, trade);
                                }
                            }
                        }

                        errorCode = BrokerErrorCode.Success;
                    }
                    catch (Exception ex)
                    {
                        Trace("Error:" + ex.Message + "\nStacktrace:" + ex.StackTrace);
                        Trace(string.Format("Retrying {0} out of {1}", retryCount, maxRetryCount));

                        if (retryCount >= maxRetryCount)
                            break;
                    }

                return errorCode;
            }
        }


        //EXCHANGE, TOKEN, SYMBOL, PRODUCT, ORDER_TYPE, DURATION, PRICE, TRIGGER_PRICE, QUANTITY, DISCLOSED_QUANTITY, TRANSACTION_TYPE, AVERAGE_PRICE,TRADED_QUANTITY, (13)...
        //...MESSAGE, EXCHANGE_ORDER_ID, PARENT_ORDER_ID, ORDER_ID, EXCHANGE_TIME, TIME_IN_MICRO, STATUS, IS_AMO, VALID_DATE, ORDER_REQUEST_ID   (23 total field count)
        public BrokerErrorCode GetOrderBook(bool newOrdersOnly, bool bOnlyOutstandingOrders, string stockCode, out Dictionary<string, EquityOrderBookRecord> orders)
        {
            lock (lockSingleThreadedUpstoxCall)
            {
                BrokerErrorCode errorCode = BrokerErrorCode.Unknown;
                orders = new Dictionary<string, EquityOrderBookRecord>();
                int retryCount = 0;
                int maxRetryCount = 3;

                while (errorCode != BrokerErrorCode.Success && retryCount++ < maxRetryCount)
                    try
                    {
                        var response = upstox.GetOrderBook();

                        string[] lines = response.Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.None);

                        for (int i = 1; i < lines.Length; i++)
                        {
                            var line = lines[i].Split(',');

                            if (line.Length < 23)
                                continue;

                            if (!string.IsNullOrEmpty(stockCode) &&
                                !line[2].Equals(stockCode, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            var order = new EquityOrderBookRecord();
                            order.OrderId = line[16];
                            var status = line[19];

                            order.Status = ParseOrderStatus(status);
                            order.Direction = line[10] == "B" ? OrderDirection.BUY : OrderDirection.SELL;
                            var milliseconds = long.Parse(line[18])/1000;
                            order.DateTime = ((new DateTime(1970, 1, 1)).AddMilliseconds(milliseconds)).ToLocalTime();
                            //order.DateTime = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).LocalDateTime;
                            order.Quantity = int.Parse(line[8]);
                            order.ExecutedQty = int.Parse(line[12]);
                            order.Price = double.Parse(line[6]);
                            order.StockCode = line[2];
                            order.Exchange = line[0];

                            if (!bOnlyOutstandingOrders ||
                                order.Status == OrderStatus.PARTEXEC ||
                                order.Status == OrderStatus.QUEUED ||
                                order.Status == OrderStatus.REQUESTED ||
                                order.Status == OrderStatus.ORDERED)
                            {
                                lock (lockObjectEquity)
                                {
                                    if (mEquityOrderBook.ContainsKey(order.OrderId))
                                    {
                                        if (newOrdersOnly)
                                        {
                                        }
                                        else
                                        {
                                            orders.Add(order.OrderId, order);
                                        }
                                        // Update the order
                                        mEquityOrderBook[order.OrderId] = order;
                                    }
                                    else
                                    {
                                        mEquityOrderBook.Add(order.OrderId, order);
                                        orders.Add(order.OrderId, order);
                                    }
                                }
                            }
                        }

                        errorCode = BrokerErrorCode.Success;
                    }
                    catch (Exception ex)
                    {
                        Trace("Error:" + ex.Message + "\nStacktrace:" + ex.StackTrace);
                        Trace(string.Format("Retrying {0} out of {1}", retryCount, maxRetryCount));

                        if (retryCount >= maxRetryCount)
                            break;
                    }

                return errorCode;
            }
        }

        public BrokerErrorCode CancelOrder(string orderId)
        {
            lock (lockSingleThreadedUpstoxCall)
            {
                BrokerErrorCode errorCode = BrokerErrorCode.Unknown;
                try
                {
                    var status = upstox.CancelSimpleOrder(orderId);

                    if (status.Contains("Cancellation sent for"))
                        errorCode = BrokerErrorCode.Success;
                    else
                    {
                        Trace(string.Format("Cancellation failed for order: {0} with status {1}", orderId, status));
                    }
                }
                catch (Exception ex)
                {
                    errorCode = BrokerErrorCode.Unknown;
                }

                return errorCode;
            }
        }

        public void Trace(string message)
        {
            message = GetType().Name + message;
            Console.WriteLine(DateTime.Now.ToString() + " " + message);
            FileTracing.TraceOut(message);
        }

        public BrokerErrorCode ConvertToDeliveryFromMarginOpenPositions(
          string stockCode,
          int openQty,
          int toConvertQty,
          string settlementRef,
          OrderDirection ordDirection,
          string exchange)
        {
            return BrokerErrorCode.Success;
        }

        public int GetNetQty(string exchange, string stockCode)
        {
            lock (lockSingleThreadedUpstoxCall)
            {
                return upstox.GetNetQty(exchange, stockCode);
            }
        }

        public int GetBoughtQty(string exchange, string stockCode)
        {
            lock (lockSingleThreadedUpstoxCall)
            {
                return upstox.GetBoughtQty(exchange, stockCode);
            }
        }


    }
}
