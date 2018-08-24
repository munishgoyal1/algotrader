using HtmlParsingLibrary;
using HttpLibrary;
using StockTrader.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UpstoxNet;

namespace StockTrader.Brokers.UpstoxBroker
{
    public class MyUpstox
    {
        Upstox upstox = new Upstox();

        public MyUpstox()
        {
            upstox.Api_Key = "1EENJJ7o6O4JG1pWDVVpYaPsarWozbZs9NPJnek2";
            upstox.Api_Secret = "woyb0firil";
            upstox.Redirect_Url = "https://upstox.com";
        }

        public BrokerErrorCode Login()
        {
            var result = upstox.Login();
            upstox.GetAccessToken();
            upstox.GetMasterContract();

            while (!upstox.Symbol_Download_Status)
                Thread.Sleep(1000);

            var holdings = upstox.GetHoldings();
            var positions = upstox.GetPositions();
            var ltp = upstox.GetSnapLtp("NSE_EQ", "BAJFINANCE");

            var a = upstox.GetOrderIds("NSE_EQ", "BAJFINANCE", "D");
            //var b = upstox.GetOrder("NSE_EQ", "BAJFINANCE", "D");


            return result ? BrokerErrorCode.Success : BrokerErrorCode.Unknown;
            /*
            var mCookieContainer = new CookieContainer();

            var UPSTOX_LOGIN_URL = "https://api.upstox.com/index/dialog/authorize?apiKey=1EENJJ7o6O4JG1pWDVVpYaPsarWozbZs9NPJnek2&redirect_uri=https://upstox.com&response_type=code";

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

        public void LogOut() { }
        public BrokerErrorCode IsLoggedIn() { return BrokerErrorCode.Success; }
        public BrokerErrorCode CheckAndLogInIfNeeded(bool bForceLogin, bool bCheckRemoteControl = false) { return BrokerErrorCode.Success; }

        // Equity methods
        public BrokerErrorCode GetEquityLTP(Exchange exchg, string stockCode, out double ltp)
        {
            var exchgStr = exchg == Exchange.NSE ? "NSE_EQ" : "BSE_EQ";
            ltp = upstox.GetLtp(exchgStr, stockCode);
            return BrokerErrorCode.Success;
        }

        public BrokerErrorCode PlaceEquityOrder(string stockCode,
            int quantity,
            string price,
            OrderPriceType orderPriceType,
            OrderDirection orderDirection,
            EquityOrderType orderType,
            Exchange exchange,
            string settlementNumber,
            out string orderRef)
        {
            orderRef = null;
            return BrokerErrorCode.Success;
        }

        public BrokerErrorCode ModifyEquityOrder(string stockCode,
            int quantity,
            string price,
            OrderPriceType orderPriceType,
            OrderDirection orderDirection,
            EquityOrderType orderType,
            Exchange exchange,
            string settlementNumber,
            out string orderRef)
        {
            orderRef = null;
            return BrokerErrorCode.Success;
        }

        public BrokerErrorCode GetHoldings(string stockCode, out List<EquityDematHoldingRecord> holdings)
        {
            holdings = null;
            return BrokerErrorCode.Success;
        }

        public BrokerErrorCode GetOrderBook(string stockCode, out List<EquityDematHoldingRecord> holdings)
        {
            holdings = null;
            upstox.GetOrderBook();

            return BrokerErrorCode.Success;
        }
        //GetPositions()

        public BrokerErrorCode GetTradeBook(string stockCode, out Dictionary<string, EquityTradeBookRecord> trades)
        {
            trades = null;
            return BrokerErrorCode.Success;
        }

        public BrokerErrorCode CancelOrder(string orderId)
        {
            return BrokerErrorCode.Success;
        }

        public BrokerErrorCode CancelOrders(string[] orderIds)
        {
            return BrokerErrorCode.Success;
        }

        public BrokerErrorCode GetBalance(out double fundAvailable)
        {
            fundAvailable = 0;
            return BrokerErrorCode.Success;
        }
    }
}
