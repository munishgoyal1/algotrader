using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using HtmlParsingLibrary;
using HttpLibrary;
using StockTrader.Core;
using StockTrader.Platform.Logging;
using StockTrader.Utilities;

namespace StockTrader.Broker.IciciDirect
{
    public partial class IciciDirectBroker
    {
        CookieContainer mCookieContainer = new CookieContainer();

        bool _fatalNoTryLogin = false;

        // lock on login refresh time  
        object lockLoginRefreshTime = new object();
        // lock on login method  
        object lockLoginMethod = new object();
        private DateTime mLastLoginRefreshTime;
        // Account related values
        string FML_ORD_TRD_DT_BSE_value;
        string FML_ORD_TRD_DT_NSE_value;
        string FML_TRADING_LIMIT_NSE_value;
        string FML_TRADING_LIMIT_BSE_value;
        string GTCDate;
        string GTCDateHidden7;
        string GTCDate7;
        string ACCOUNTNUM;
        string NSEStatus;
        string BSEStatus;
        string FML_PRCNTG_CHECK;
        string FML_XCHNG_ST;

        string mUsername, mPassword, mDOB;

        string _noLoginNonNullResponse = "<script language=\"javascript\"> //if (EscK == \"\") //  EscK = ' or Esc Key'; var SelectedTradingFolder;" +
    "</script> <script language=\"javascript\"> var chttps chttps = \"https://secure.icicidirect.com/NewSiteTrading\";" +
    "location.href=chttps + \"/customer/logon.asp\"</script>";

        // Important urls
        const string URL_ICICI_LOGON = @"https://secure.icicidirect.com/IDirectTrading/customer/login.aspx";
        const string URL_ICICI_LOGON_REFERRER = @"https://secure.icicidirect.com/IDirectTrading/customer/login.aspx";
        const string URL_ICICI_LOGOUT = @"https://secure.icicidirect.com/IDirectTrading/customer/logout.aspx";
        const string URL_ICICI_MODIFYALLOCATION = @"https://secure.icicidirect.com/IDirectTrading/Trading/Equity/EquityHandler.ashx?faprod=E";

        // EQUITY
        const string URL_ICICI_EQT_QUOTE = @"http://getquote.icicidirect.com/trading_stock_quote.aspx?Symbol=";
        const string URL_ICICI_EQT_SPREAD = @"https://secure.icicidirect.com/IDirectTrading/trading/trading_stock_bestbid.aspx?Symbol=";//RELIND";
        const string URL_ICICI_REFERRER = @"https://secure.icicidirect.com/IDirectTrading/Trading/Trade.aspx";
        const string URL_ICICI_BASE_ACTION = @"https://secure.icicidirect.com/IDirectTrading/Trading/Equity/EquityHandler.ashx";
        const string URL_ICICI_EQT_ORDERBOOK = URL_ICICI_BASE_ACTION;  //POST pgname=eqOrdBook&ismethodcall=0&mthname=
        const string URL_ICICI_EQT_TRADEBOOK = URL_ICICI_BASE_ACTION;  //POST pgname=eqTrdBook&ismethodcall=0&mthname=
        const string URL_ICICI_EQT_FASTBUYSELL = URL_ICICI_BASE_ACTION;
        const string URL_ICICI_EQT_CANCEL_ORDER = URL_ICICI_BASE_ACTION;

        HashSet<string> ServerLoginTimeoutRefreshURLs = new HashSet<string>()
        {
            URL_ICICI_LOGON,
            URL_ICICI_MODIFYALLOCATION,
            URL_ICICI_BASE_ACTION
        };


        bool mLoggedIn;

        public IciciDirectBroker(string username,
            string password, string dob)
        {
            mUsername = username;
            mPassword = password;
            mDOB = dob;

            //noLoginNonNullResponse = noLoginNonNullResponse.Replace(" ", "").Replace("\n", "").Replace("\t", "");
            _noLoginNonNullResponse = Regex.Replace(_noLoginNonNullResponse, @"\s", "");
        }
        /////////////////////////////////////////////////////
        //////      ACCOUNT LOGIN FUNCTIONALITY       ///////
        ///////////////////////////////////////////////////// 

        // NOTE: Should be used only when logged-in status is mandatory
        // so that server contact time (which matters to maintain the login) can be updated
        // Do not use this (Use HttpHelper.GetWebPageResponse instaed) for GET QUOTE or similar where log-in is not needed

        private string IciciGetWebPageResponse(string url,
            string postdata,
            string referer,
            CookieContainer cookieContainer,
            out BrokerErrorCode errorCode)
        {
            errorCode = BrokerErrorCode.Success;
            string data = HttpHelper.GetWebPageResponse(url,
                postdata,
                referer,
                cookieContainer);

            // refresh the time if data is valid login and url is of refresh type
            errorCode = CheckResponseDataValidity(data);
            if (ServerLoginTimeoutRefreshURLs.Contains(url) && errorCode.Equals(BrokerErrorCode.Success))
            {
                RefreshLastServerContactTime();
            }
            return data;
        }

        // get important account related values once during login
        BrokerErrorCode GetImportantValues()
        {
            BrokerErrorCode errorCode = BrokerErrorCode.NotLoggedIn;

            var equityHomePageData = IciciGetWebPageResponse(URL_ICICI_BASE_ACTION,
                      "pgname=eqhome&ismethodcall=0&mthname=",
                      URL_ICICI_REFERRER,
                      mCookieContainer,
                      out errorCode);

            var equityFastBuySellPageData = IciciGetWebPageResponse(URL_ICICI_EQT_FASTBUYSELL,
                      "pgname=eqfastbuy&ismethodcall=0&mthname=",
                      URL_ICICI_REFERRER,
                      mCookieContainer,
                      out errorCode);

            if (errorCode.Equals(BrokerErrorCode.Success))
            {
                FML_ORD_TRD_DT_BSE_value = StringParser.GetStringBetween(equityFastBuySellPageData,
                    0,
                    "<input type=\"hidden\" id=\"FML_ORD_TRD_DT_BSE\" value=\"",
                    "\"",
                    null);
                FML_ORD_TRD_DT_NSE_value = StringParser.GetStringBetween(equityFastBuySellPageData,
                   0,
                   "<input type=\"hidden\" id=\"FML_ORD_TRD_DT_NSE\" value=\"",
                   "\"",
                   null);
                GTCDate = StringParser.GetStringBetween(equityFastBuySellPageData,
                  0,
                  "<input type=\"hidden\" name=\"GTCDate\" id=\"GTCDate\" value=\"",
                  "\"",
                  null);
                GTCDate7 = StringParser.GetStringBetween(equityFastBuySellPageData,
                  0,
                  "<input id=\"GTCDate7\" type=\"text\" style=\"width: 65px !important;\" value=\"",
                  "\"",
                  null);
                GTCDateHidden7 = StringParser.GetStringBetween(equityFastBuySellPageData,
                  0,
                  "<input type=\"hidden\" name=\"GTCDateHidden7\" id=\"GTCDateHidden7\" value=\"",
                  "\"",
                  null);
                NSEStatus = StringParser.GetStringBetween(equityFastBuySellPageData,
                   0,
                   "<input type=\"hidden\" id=\"NSEStatus\" value=\"",
                   "\"",
                   null);
                BSEStatus = StringParser.GetStringBetween(equityFastBuySellPageData,
                   0,
                   "<input type=\"hidden\" id=\"BSEStatus\" value=\"",
                   "\"",
                   null);
                FML_XCHNG_ST = StringParser.GetStringBetween(equityFastBuySellPageData,
                   0,
                   "<input type=\"hidden\" id=\"FML_XCHNG_ST\" value=\"",
                   "\"",
                   null);
                FML_PRCNTG_CHECK = StringParser.GetStringBetween(equityFastBuySellPageData,
                   0,
                   "<input type=\"hidden\" id=\"FML_PRCNTG_CHECK\" value=\"",
                   "\"",
                   null);
                ACCOUNTNUM = StringParser.GetStringBetween(equityFastBuySellPageData,
                    0,
                    "A/C No:",
                    "')",
                    null);
            }

            return errorCode;
        }

        private BrokerErrorCode CheckResponseDataValidity(string isLoggedInCheckData)
        {
            BrokerErrorCode errorCode = BrokerErrorCode.Success;
            if (String.IsNullOrEmpty(isLoggedInCheckData))
            {
                errorCode = BrokerErrorCode.NullResponse;
            }
            else if (isLoggedInCheckData.IndexOf("have faced some technical or connectivity") > -1)
                errorCode = BrokerErrorCode.TechnicalReason;
            else if (isLoggedInCheckData.IndexOf("close your browser and try again") > -1)
                errorCode = BrokerErrorCode.TechnicalReason;
            else if ((isLoggedInCheckData.IndexOf("Invalid User:The Login Id entered is not valid", StringComparison.OrdinalIgnoreCase) >= 0) ||
                (isLoggedInCheckData.IndexOf("Bad connection", StringComparison.OrdinalIgnoreCase) >= 0) ||
                (isLoggedInCheckData.IndexOf("login id", StringComparison.OrdinalIgnoreCase) > 0) ||
                (isLoggedInCheckData.IndexOf("Invalid User", StringComparison.OrdinalIgnoreCase) >= 0) ||
                (isLoggedInCheckData.IndexOf(/*"Session has */"Timed Out", StringComparison.OrdinalIgnoreCase) >= 0) ||
                (isLoggedInCheckData.IndexOf("Please Login again", StringComparison.OrdinalIgnoreCase) >= 0) ||
                (isLoggedInCheckData.Length < 2000 && isLoggedInCheckData.IndexOf("location.href=chttps + \"/customer/logon.asp\"", StringComparison.OrdinalIgnoreCase) >= 0)
                )
            {
                mLoggedIn = false;
                errorCode = BrokerErrorCode.NotLoggedIn;
            }
            else
            {
                //resultString = Regex.Replace(subjectString, @"(?:(?:\r?\n)+ +){2,}", @"\n");
                string isLoggedInCheckData1 = Regex.Replace(isLoggedInCheckData, @"\s", "");
                //isLoggedInCheckData = isLoggedInCheckData.Replace(" ", "").Replace("\n", "").Replace("\t", "");
                if (isLoggedInCheckData1 == _noLoginNonNullResponse)
                {
                    errorCode = BrokerErrorCode.NotLoggedIn;
                    mLoggedIn = false;
                }
            }

            if (errorCode != BrokerErrorCode.Success)
            {
                string desc = string.Format("CheckResponseValidity Failed-Code-{0}", errorCode);
                string content = string.Format("<b>{0}\n\n\n\n{1}\n<b>", desc, isLoggedInCheckData);
                File.WriteAllText(SystemUtils.GetErrorFileName(desc, true), content);
            }

            return errorCode;
        }

        // Is logged in
        public BrokerErrorCode IsLoggedIn()
        {
            BrokerErrorCode errorCode = TouchServer();

            if (errorCode.Equals(BrokerErrorCode.Success))
                mLoggedIn = true;

            return errorCode;
        }

        // Do login
        public BrokerErrorCode LogIn()
        {
            if (_fatalNoTryLogin)
            {
                FileTracing.TraceOut(Thread.CurrentThread.Name + "ERROR: Not trying Actual LogIn() because now in FatalNoLoginTry mode.");
                return BrokerErrorCode.FatalNoLoginTry;
            }

            BrokerErrorCode errorCode = IsLoggedIn();
            if (!errorCode.Equals(BrokerErrorCode.Success))
            {
                mLoggedIn = false;
                //FileTracing.TraceOut(Thread.CurrentThread.Name + ": In Actual LogIn()");

                var postData =
                    string.Format("ctl00%24Hearder1%24drpStock=India&ctl00%24Hearder1%24txtTopSearch=Stock+Name+%2F+Stock+Code&ctl00%24Hearder1%24hidHandler=https%3A%2F%2Fsecure.icicidirect.com%2FIDirectTrading%2FBasemasterpage%2FHeaderDataHandler.ashx&__EVENTTARGET=lbtLogin&__EVENTARGUMENT=&npage=&ctl00%24ContentPlaceHolder1%24hisreen=1366x768&ctl00%24ContentPlaceHolder1%24lms_lnk=&ctl00%24ContentPlaceHolder1%24txtUserId={0}&ctl00%24ContentPlaceHolder1%24txtu=&ctl00%24ContentPlaceHolder1%24m_StartIn_Fld=&ctl00%24ContentPlaceHolder1%24txtPass={1}&ctl00%24ContentPlaceHolder1%24txtDOB={2}&ctl00%24ContentPlaceHolder1%24drpTrade=12&ctl00%24Footer1%24showplus=&ctl00%24hidIsTrade=1&__VIEWSTATEGENERATOR=241519BE",
                    mUsername, mPassword, mDOB);

                // re-login
                mCookieContainer = new CookieContainer();
                // This GET is to populate session id in cookies
                string initGetResponse = HttpHelper.GetWebPageResponse(URL_ICICI_LOGON, null,
                    URL_ICICI_LOGON_REFERRER,
                    mCookieContainer);

                string loginResponse = HttpHelper.GetWebPageResponse(URL_ICICI_LOGON, postData,
                    URL_ICICI_LOGON_REFERRER,
                    mCookieContainer);

                if (String.IsNullOrEmpty(loginResponse))
                    errorCode = BrokerErrorCode.Http;

                else if ((loginResponse.IndexOf("You last accessed the site", StringComparison.OrdinalIgnoreCase) >= 0) || (IsLoggedIn() == BrokerErrorCode.Success))
                {
                    errorCode = GetImportantValues();
                    if (errorCode.Equals(BrokerErrorCode.Success))
                    {
                        mLoggedIn = true;
                        RefreshLastServerContactTime();
                    }
                }
                else
                {
                    if (loginResponse.IndexOf("customer_change_password", StringComparison.OrdinalIgnoreCase) >= 0) errorCode = BrokerErrorCode.ChangePassword;
                    if (loginResponse.IndexOf("echnical reason", StringComparison.OrdinalIgnoreCase) >= 0) errorCode = BrokerErrorCode.TechnicalReason;
                    else if (loginResponse.IndexOf("Locked", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _fatalNoTryLogin = true;
                        errorCode = BrokerErrorCode.Locked;
                    }
                    else if (loginResponse.IndexOf("Invalid Login Id or Password", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _fatalNoTryLogin = true;
                        errorCode = BrokerErrorCode.InvalidLoginPassword;
                    }
                    else errorCode = BrokerErrorCode.Unknown;
                }
            }

            return errorCode;
        }

        // Check Last Refresh time & if past 3 minutes then login
        // Even if someone else acquired login Session by weblogin etc.
        // Still this should work as the LastLoginRefreshTime becomes stale eventually and 
        // resulting into relogin trigger

        // Keep calling this in a separate thread 
        public BrokerErrorCode CheckAndLogInIfNeeded(bool bForceLogin, bool bCheckRemoteControl = false)
        {
            TimeSpan maxTimeWithoutRefresh = new TimeSpan(0, 3, 0);
            BrokerErrorCode errorCode = TouchServer();

            lock (lockLoginMethod)
            {
                if (bForceLogin || (errorCode != BrokerErrorCode.Success) || ((DateTime.Now - LastLoginRefreshTime) > maxTimeWithoutRefresh))
                {
                    ProgramRemoteControl remoteControlValue = ProgramRemoteControl.RUN;

                    if (bCheckRemoteControl)
                    {
                        remoteControlValue = RemoteUtils.GetProgramRemoteControlValue();
                    }
                    // keep looping until PAUSE status is reset
                    if (remoteControlValue.Equals(ProgramRemoteControl.PAUSE) ||
                        remoteControlValue.Equals(ProgramRemoteControl.STOP) ||
                        remoteControlValue.Equals(ProgramRemoteControl.HIBERNATE))
                    {
                        string traceString = string.Format("CheckAndLogInIfNeeded: Remote Control {0} the LOGIN\n", remoteControlValue.ToString());
                        FileTracing.TraceOut(traceString);
                        errorCode = BrokerErrorCode.RemotePausedOrStopped;
                    }
                    else
                    {
                        string traceString = string.Format("CheckAndLogInIfNeeded: Need to do actual LogIn. ");
                        errorCode = LogIn();
                        traceString += "LogIn() returned: " + errorCode.ToString();
                        FileTracing.TraceOut(traceString);
                    }
                }
            }
            return errorCode;
        }


        public BrokerErrorCode TouchServer()
        {
            BrokerErrorCode errorCode = BrokerErrorCode.Success;

            string data = IciciGetWebPageResponse(URL_ICICI_MODIFYALLOCATION,
                "pgname=ModAlloc&ismethodcall=0&mthname=",
                URL_ICICI_REFERRER,
                mCookieContainer,
                out errorCode);

            if (string.IsNullOrEmpty(data))
                return BrokerErrorCode.NullResponse;

            if (data.Contains("Bank Account No. "))
                return BrokerErrorCode.Success;

            //if (data == "Y<script> window.location.href = 'https://secure.icicidirect.com/IDirectTrading/customer/login.aspx'</script>")
            return BrokerErrorCode.NotLoggedIn;
        }

        private DateTime LastLoginRefreshTime
        {
            get
            {
                lock (lockLoginRefreshTime)
                {
                    return mLastLoginRefreshTime;
                }
            }
            set
            {
                lock (lockLoginRefreshTime)
                {
                    mLastLoginRefreshTime = value;
                }
            }
        }

        private void RefreshLastServerContactTime()
        {
            LastLoginRefreshTime = DateTime.Now;
        }
        public void LogOut()
        {
            mLoggedIn = false;
            FileTracing.TraceOut("LogOut");
            HttpHelper.GetWebPageResponse(URL_ICICI_LOGOUT,
                null,
                null,
                mCookieContainer);
            LastLoginRefreshTime = DateTime.MinValue;
        }


        #region IDisposable Members
        void IDisposable.Dispose()
        {
            LogOut();
            ClearTradeReferenceNumbers(TradingSectionType.DERIVATIVES);
            ClearTradeReferenceNumbers(TradingSectionType.EQUITY);
            ClearOrderReferenceNumbers(TradingSectionType.EQUITY);
        }


        #endregion
    }
}