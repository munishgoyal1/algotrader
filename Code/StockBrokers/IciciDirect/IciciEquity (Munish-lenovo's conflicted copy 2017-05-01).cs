using System;
using System.Collections.Generic;
using System.Threading;
using HtmlParsingLibrary;
using HttpLibrary;
using StockTrader.Core;
using StockTrader.Utilities;
using StockTrader.Utilities.Broker;


namespace StockTrader.Broker.IciciDirect
{
    public partial class IciciDirectBroker
    {
        // place margin or delivery order
        public BrokerErrorCode PlaceEquityMarginDeliveryFBSOrder(string stockCode,
            int quantity,
            string price,
            OrderPriceType orderPriceType,
            OrderDirection orderDirection,
            EquityOrderType orderType,
            Exchange exchange,
            out string orderRef)
        {
            orderRef = "";

            // Login If needed
            BrokerErrorCode errorCode = CheckAndLogInIfNeeded(false);
            if (errorCode != BrokerErrorCode.Success)
                return errorCode;

            string FML_ORD_ORDR_FLW_value = orderDirection == OrderDirection.BUY ? "B" : "S";

            string FML_ORD_TYP_value = null, FML_ORD_LMT_RT_value = String.Empty;
            if (orderPriceType == OrderPriceType.MARKET)
            {
                FML_ORD_TYP_value = "M";
            }
            else if (orderPriceType == OrderPriceType.LIMIT)
            {
                FML_ORD_TYP_value = "L";
                FML_ORD_LMT_RT_value = price;
            }

            string FML_ORD_TRD_DT_value = null;
            string squareOffMode = orderDirection == OrderDirection.SELL ? "S" : "M";
            string FML_ORD_XCHNG_CD_value = null;
            if (exchange == Exchange.BSE)
            {
                FML_ORD_TRD_DT_value = FML_ORD_TRD_DT_BSE_value;
                FML_ORD_XCHNG_CD_value = "BSE";
            }
            else if (exchange == Exchange.NSE)
            {
                FML_ORD_TRD_DT_value = FML_ORD_TRD_DT_NSE_value;
                FML_ORD_XCHNG_CD_value = "NSE";
            }

            string prdctType = orderType == EquityOrderType.DELIVERY ? "C" : "M";

            string query =
                "&FML_SQ_FLAG=" + (orderType == EquityOrderType.MARGIN ? squareOffMode : "") +  //Squareoff mode: M (for client) S (for broker), for BSE only S
                "&ORD_XCHNG_CD=" + FML_ORD_XCHNG_CD_value +
                "&FML_STCK_CD=" + stockCode +
                "&FML_QTY=" + quantity.ToString() +
                "&FML_ORD_DSCLSD_QTY=" +
                "&FML_POINT_TYPE=T" +
                "&GTCDate7=" + GTCDate7 +
                "&GTCDate=" + GTCDate +
                "&GTCDateHidden7=" + GTCDateHidden7 +
                "&FML_ORD_TYP=" + FML_ORD_TYP_value +
                "&FML_ORD_LMT_RT=" + FML_ORD_LMT_RT_value +
                "&FML_GMS_CSH_PRDCT_PRCNTG=" +
                "&FML_ORD_STP_LSS=" +
                "&FML_TRADING_PASSWD=" +
                "&Submit=Buy+Now" +
                "&FML_ORD_TRD_DT_BSE=" + FML_ORD_TRD_DT_BSE_value +
                "&FML_ORD_TRD_DT_NSE=" + FML_ORD_TRD_DT_NSE_value +
                "&FML_ORD_XCHNG_CD=" + FML_ORD_XCHNG_CD_value +
                "&NSEStatus=" + NSEStatus +
                "&BSEStatus=" + BSEStatus +
                "&FML_PRCNTG_CHECK=" + FML_PRCNTG_CHECK +
                "&FML_ORD_TRD_DT=" + FML_ORD_TRD_DT_value +
                "&FML_XCHNG_ST=" + NSEStatus +  // O or C  (open or closed)
                "&NicValue=" +
                "&FML_LAS=" +
                "&FML_ACCOUNT=" +
                "&FML_URQ_USR_RD_FLG=" +
                "&FML_ORD_PRDCT_TYP=" + prdctType +
                "&FML_ORD_ORDR_FLW=" + FML_ORD_ORDR_FLW_value +
                "&pgname=eqfastbuy&ismethodcall=1&mthname=DoFinalSubmit";

            string orderPlacePageData = IciciGetWebPageResponse(URL_ICICI_EQT_FASTBUYSELL,
                query,
                URL_ICICI_REFERRER,
                mCookieContainer,
                out errorCode);

            if (errorCode.Equals(BrokerErrorCode.Success))
            {
                errorCode = GetEQTOrderPlacementCode(orderPlacePageData);
                if (BrokerErrorCode.Success == errorCode)
                {
                    orderRef = ExtractOrderReferenceNumber(orderPlacePageData);
                }
            }
            return errorCode;
        }


        // place margin pending delivery square off order
        public BrokerErrorCode PlaceEquityMarginSquareOffOrder(string stockCode,
            int quantity,
            string price,
            OrderPriceType orderPriceType,
            OrderDirection orderDirection,
            EquityOrderType orderType,
            Exchange exchange,
            out string orderRef)
        {
            orderRef = "";

            // Login If needed
            BrokerErrorCode errorCode = CheckAndLogInIfNeeded(false);
            if (errorCode != BrokerErrorCode.Success)
                return errorCode;

            string FML_ORD_ORDR_FLW_value = orderDirection == OrderDirection.BUY ? "B" : "S";
            string FML_XCHNG_SGMNT_CD = "";

            string FML_ORD_TYP_value = null, FML_ORD_LMT_RT_value = String.Empty;
            if (orderPriceType == OrderPriceType.MARKET)
            {
                FML_ORD_TYP_value = "M";
            }
            else if (orderPriceType == OrderPriceType.LIMIT)
            {
                FML_ORD_TYP_value = "L";
                FML_ORD_LMT_RT_value = price;
            }

            string FML_ORD_TRD_DT_value = null;
            string squareOffMode = orderDirection == OrderDirection.SELL ? "S" : "M";
            string FML_ORD_XCHNG_CD_value = null;
            if (exchange == Exchange.BSE)
            {
                FML_XCHNG_SGMNT_CD = "B";
                FML_ORD_TRD_DT_value = FML_ORD_TRD_DT_BSE_value;
                FML_ORD_XCHNG_CD_value = "BSE";
            }
            else if (exchange == Exchange.NSE)
            {
                FML_XCHNG_SGMNT_CD = "N";
                FML_ORD_TRD_DT_value = FML_ORD_TRD_DT_NSE_value;
                FML_ORD_XCHNG_CD_value = "NSE";
            }

            string prdctType = orderType == EquityOrderType.DELIVERY ? "C" : "M";

            string query =
                "&FML_ORD_XCHNG_CD=" + FML_ORD_XCHNG_CD_value +
                "&FML_STCK_CD=" + stockCode +
                "&FML_QTY=" + quantity.ToString() +
                "&FML_XCHNG_SGMNT_CD=" + FML_XCHNG_SGMNT_CD +
                "&FML_STTLMNT_NMBR=" +
                "&AvgPrc=" +
                "&FML_ORD_TRD_DT=" + FML_ORD_TRD_DT_NSE_value +
                "&FML_XCHNG_ST=" + NSEStatus +
                "&NicValue=" +
                "&m_IntPass=1" +
                "&FML_URQ_USR_RD_FLG=" +
                "&FML_ORD_ORDR_FLW=" + FML_ORD_ORDR_FLW_value +
                "&FML_ORD_DSCLSD_QTY=" +
                "&FML_SQROFF=" + quantity.ToString() +
                "&FML_POINT_TYPE=T" +
                "&FML_ORD_TYP=" + FML_ORD_TYP_value +
                "&FML_LM_RT=" + FML_ORD_LMT_RT_value +
                "&FML_GMS_CSH_PRDCT_PRCNTG=" +
                "&FML_ORD_STP_LSS=" +
                "&Submit1=Square Off" +
                "&Submit2=Clear" +
                "&FML_ORD_XCHNG_SGMNT_STTLMNT=" +
                "&FML_QUOTE=" +
                "&FML_QUOTE_TIME=" +
                "&FML_TRDNG_PSSWRD_FLG=N" +
                "&FML_RT=" +
                "&FML_ISIN_NMBR=" +
                "&FML_STK_STCK_NM=" +
                "&FML_TM=" +
                "&FML_QTY_OTH=" + quantity.ToString() +
                "&pgname=EquityPendingForDeliverySquareOff" +
                "&ismethodcall=1" +
                "&mthname=ValidateFormData";

            string orderPlacePageData = IciciGetWebPageResponse(URL_ICICI_BASE_ACTION,
                query,
                URL_ICICI_REFERRER,
                mCookieContainer,
                out errorCode);

            if (errorCode.Equals(BrokerErrorCode.Success))
            {
                errorCode = GetEQTOrderPlacementCode(orderPlacePageData);
                if (BrokerErrorCode.Success == errorCode)
                {
                    orderRef = ExtractOrderReferenceNumberMarginSqOffOrder(orderPlacePageData);
                }
            }
            return errorCode;
        }


        public BrokerErrorCode ConvertToDeliveryFromPendingForDelivery(string stockCode,
          int openQty,
          int toConvertQty,
          string settlementRef,
          Exchange exchange)
        {
            // Login If needed
            BrokerErrorCode errorCode = CheckAndLogInIfNeeded(false);
            if (errorCode != BrokerErrorCode.Success)
                return errorCode;

            string FML_XCHNG_SGMNT_CD = "";
            string FML_ORD_XCHNG_CD_value = null;

            if (exchange == Exchange.BSE)
            {
                FML_XCHNG_SGMNT_CD = "B";
                FML_ORD_XCHNG_CD_value = "BSE";
            }
            else if (exchange == Exchange.NSE)
            {
                FML_XCHNG_SGMNT_CD = "N";
                FML_ORD_XCHNG_CD_value = "NSE";
            }

            string query =
                "&FML_QTY=" + toConvertQty.ToString() +
                "&NicValue=" +
                "&FML_ORD_XCHNG_CD=" + FML_ORD_XCHNG_CD_value +
                "&FML_STCK_CD=" + stockCode +
                "&FML_OPEN_QTY=" + openQty.ToString() +
                "&FML_XCHNG_SGMNT_CD=" + FML_XCHNG_SGMNT_CD +
                "&FML_ORD_XCHNG_SGMNT_STTLMNT=" + settlementRef +
                "&Submit1=Convert+Now" +
                "&pgname=EquityPendingForDeliveryConvertToDelv" +
                "&ismethodcall=0" +
                "&mthname=";

            string orderPlacePageData = IciciGetWebPageResponse(URL_ICICI_BASE_ACTION,
                query,
                URL_ICICI_REFERRER,
                mCookieContainer,
                out errorCode);

            if (errorCode.Equals(BrokerErrorCode.Success))
            {
                errorCode = GetEQTOrderPlacementCode(orderPlacePageData);
                if (BrokerErrorCode.Success == errorCode)
                {
                    if (orderPlacePageData.IndexOf("You have successfully converted the specified quantity to delivery") > -1)
                        errorCode = BrokerErrorCode.Success;
                    else
                        errorCode = BrokerErrorCode.Unknown;
                }
            }
            return errorCode;
        }

        //////////////////////////////////////////
        //////      GET EQUITY QUOTE       //////
        ////////////////////////////////////////                 
        // NOTE: We dont want to use IciciGetWebPageResponse here since it updates the login refresh time
        // whereas getting equity quote doesnt actually refresh contact time with server
        // it doesnt even need us to be logged in
        public BrokerErrorCode GetEquityQuote(string stockCode, out EquitySymbolQuote[] info)
        {
            BrokerErrorCode errorCode = BrokerErrorCode.Success;
            info = new EquitySymbolQuote[2];

            string quoteData = null;
            int retryCount = 0;
            do
            {
                quoteData = HttpHelper.GetWebPageResponse(
                    URL_ICICI_EQT_QUOTE + stockCode.ToUpper(),
                     null,
                     null,
                     mCookieContainer);
                retryCount++;
            } while (quoteData == null && retryCount < 5);

            if (string.IsNullOrEmpty(quoteData) || quoteData.IndexOf("entered is not valid") > -1)
            {
                // web problems, slow connection, server down etc.
                return BrokerErrorCode.NullResponse;
            }

            quoteData = quoteData.Substring(quoteData.IndexOf("Best 5 Bids/Offers", 0));

            string subQuoteData = StringParser.GetStringBetween(quoteData,
                0,
                "<table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" class=\"smallfont1\">",
                "</table>",
                new string[] { });

            ParsedTable table = (ParsedTable)HtmlTableParser.ParseHtmlIntoTables("<table>" + subQuoteData + "</table>", true);

            DateTime result = DateTime.Now;
            bool bSuccess = false;

            // NSE price info
            info[0] = new EquitySymbolQuote();

            info[0].StockCode = stockCode;
            info[0].Exchange = Exchange.NSE;
            bSuccess = DateTime.TryParse(table[1, 4] + " " + table[2, 4], out result);
            if (bSuccess)
                info[0].QuoteTime = result;

            info[0].LastTradePrice = table[1, 1].ToString().Trim();
            info[0].OpenPrice = table[3, 1].ToString().Trim();
            info[0].HighPrice = table[4, 1].ToString().Trim();
            info[0].LowPrice = table[5, 1].ToString().Trim();
            info[0].PreviousClosePrice = table[6, 1].ToString().Trim();
            info[0].PercentageChange = table[8, 1].ToString().Trim();
            info[0].VolumeTraded = table[11, 1].ToString().Trim();

            info[0].BestBidPrice = table[3, 4].ToString().Trim();
            info[0].BestOfferPrice = table[4, 4].ToString().Trim();
            info[0].BestBidQty = table[5, 4].ToString().Trim();
            info[0].BestOfferQty = table[6, 4].ToString().Trim();
            info[0].Price52WkHigh = table[7, 4].ToString().Trim();
            info[0].Price52WkLow = table[8, 4].ToString().Trim();


            // BSE price info
            info[1] = new EquitySymbolQuote();

            info[1].StockCode = stockCode;
            info[1].Exchange = Exchange.BSE;
            bSuccess = DateTime.TryParse(table[1, 5] + " " + table[2, 5], out result);
            if (bSuccess)
                info[1].QuoteTime = result;

            info[1].LastTradePrice = table[1, 2].ToString();
            info[1].OpenPrice = table[3, 2].ToString();
            info[1].HighPrice = table[4, 2].ToString();
            info[1].LowPrice = table[5, 2].ToString();
            info[1].PreviousClosePrice = table[6, 2].ToString();
            info[1].PercentageChange = table[8, 2].ToString();
            info[1].VolumeTraded = table[11, 2].ToString();

            info[1].BestBidPrice = table[3, 5].ToString();
            info[1].BestOfferPrice = table[4, 5].ToString();
            info[1].BestBidQty = table[5, 5].ToString();
            info[1].BestOfferQty = table[6, 5].ToString();
            info[1].Price52WkHigh = table[7, 5].ToString();
            info[1].Price52WkLow = table[8, 5].ToString();

            return errorCode;
        }

        public BrokerErrorCode GetEquitySpread(string stockCode, out EquitySymbolSpread[] info)
        {
            BrokerErrorCode errorCode = BrokerErrorCode.Success;
            info = new EquitySymbolSpread[2];

            string quoteData = null;
            int retryCount = 0;
            do
            {
                quoteData = HttpHelper.GetWebPageResponse(
                    URL_ICICI_EQT_SPREAD + stockCode.ToUpper(),
                     null,
                     null,
                     mCookieContainer);
                retryCount++;
            } while (quoteData == null && retryCount < 5);

            // web problems, slow connection, server down etc.
            if (string.IsNullOrEmpty(quoteData) || quoteData.IndexOf("entered is not valid") > -1)
                return BrokerErrorCode.NullResponse;

            ParsedTable table = (ParsedTable)HtmlTableParser.ParseHtmlIntoTables(quoteData, true);

            // NSE price info
            info[0] = new EquitySymbolSpread();
            info[0].Symbol = stockCode;
            info[0].Exchange = Exchange.NSE;
            string tempStr = ParsedTable.GetValue(table, new int[] { 0, 3, 0, 1 });
            DateTime lastTradeTime = DateTime.Parse(tempStr);
            tempStr = ParsedTable.GetValue(table, new int[] { 0, 3, 1, 1 });
            lastTradeTime += TimeSpan.Parse(tempStr);
            info[0].QuoteTime = lastTradeTime;

            info[0].TotalBidQty = MarketUtils.GetVolume(ParsedTable.GetValue(table, new int[] { 0, 5, 7, 1 }));
            info[0].TotalOfferQty = MarketUtils.GetVolume(ParsedTable.GetValue(table, new int[] { 0, 5, 8, 1 }));

            for (int i = 0; i < 5; i++)
            {
                info[0].BestBidQty[i] = MarketUtils.GetVolume(ParsedTable.GetValue(table, new int[] { 0, 5, i + 2, 0 }));
                info[0].BestBidPrice[i] = MarketUtils.GetPrice(ParsedTable.GetValue(table, new int[] { 0, 5, i + 2, 1 }));

                info[0].BestOfferQty[i] = MarketUtils.GetVolume(ParsedTable.GetValue(table, new int[] { 0, 5, i + 2, 2 }));
                info[0].BestOfferPrice[i] = MarketUtils.GetPrice(ParsedTable.GetValue(table, new int[] { 0, 5, i + 2, 3 }));
            }

            // BSE price info
            info[1] = new EquitySymbolSpread();
            info[1].Symbol = stockCode;
            info[1].Exchange = Exchange.BSE;
            tempStr = ParsedTable.GetValue(table, new int[] { 0, 3, 0, 3 });
            lastTradeTime = DateTime.Parse(tempStr);
            tempStr = ParsedTable.GetValue(table, new int[] { 0, 3, 1, 3 });
            lastTradeTime += TimeSpan.Parse(tempStr);
            info[1].QuoteTime = lastTradeTime;

            info[1].TotalBidQty = MarketUtils.GetVolume(ParsedTable.GetValue(table, new int[] { 0, 5, 7, 3 }));
            info[1].TotalOfferQty = MarketUtils.GetVolume(ParsedTable.GetValue(table, new int[] { 0, 5, 8, 3 }));

            for (int i = 0; i < 5; i++)
            {
                info[1].BestBidQty[i] = MarketUtils.GetVolume(ParsedTable.GetValue(table, new int[] { 0, 5, i + 2, 4 }));
                info[1].BestBidPrice[i] = MarketUtils.GetPrice(ParsedTable.GetValue(table, new int[] { 0, 5, i + 2, 5 }));

                info[1].BestOfferQty[i] = MarketUtils.GetVolume(ParsedTable.GetValue(table, new int[] { 0, 5, i + 2, 6 }));
                info[1].BestOfferPrice[i] = MarketUtils.GetPrice(ParsedTable.GetValue(table, new int[] { 0, 5, i + 2, 7 }));
            }
            return errorCode;
        }

        //////////////////////////////////////
        //////      CANCEL ORDER       //////
        ////////////////////////////////////                      

        // IBroker.CancelEquityOrder
        public BrokerErrorCode CancelEquityOrder(string orderRef)
        {
            return CancelEquityOrder(orderRef, true);
        }

        private BrokerErrorCode CancelEquityOrder(string orderRef, bool refreshOrderBook)
        {
            // Login If needed
            BrokerErrorCode errorCode = CheckAndLogInIfNeeded(false);
            if (errorCode != BrokerErrorCode.Success)
            {
                return errorCode;
            }

            if (refreshOrderBook)
            {
                // Make sure we have the latest order book data
                errorCode = RefreshEquityOrderBookToday();
                if (errorCode != BrokerErrorCode.Success)
                {
                    return errorCode;
                }
            }

            EquityOrderBookRecord orderInfo;
            // Search for order in the Orders dictionary
            lock (lockObjectEquity)
            {
                if (mEquityOrderBook.ContainsKey(orderRef))
                {
                    orderInfo = mEquityOrderBook[orderRef];
                }
                else
                {
                    return BrokerErrorCode.OrderDoesNotExist;
                }
            }

            if (orderInfo.Status != OrderStatus.ORDERED &&
                orderInfo.Status != OrderStatus.PARTEXEC &&
                orderInfo.Status != OrderStatus.REQUESTED)
            {
                if (orderInfo.Status == OrderStatus.QUEUED)
                {
                    errorCode = BrokerErrorCode.OrderQueuedCannotCancel;
                }
                else if (orderInfo.Status == OrderStatus.EXECUTED)
                {
                    errorCode = BrokerErrorCode.OrderExecutedCannotCancel;
                }
                else
                {
                    errorCode = BrokerErrorCode.InValidOrderToCancel;
                }
                return errorCode;
            }

            // Instead of getting it actually from order book
            // We simply assume current day only
            DateTime currentDate = MarketUtils.GetMarketCurrentDate();
            string orderStatus = IciciConstants.OrderStatusString[(int)orderInfo.Status];
            string zipCode = orderRef.Substring(8, 2);

            string postData = "Submit1=Yes&FML_PASS=1&NicValue=&m_pipeId=N9" +
                "&pgname=CanOrd&ismethodcall=0&mthname=" +
                "&m_status=" + orderStatus +
                "&m_nsestatus=C" +
                "&m_bsestatus=C" +
                "&FML_ORDR_REF=" + orderRef +
                // TODO: C for CASH , currently hardcoded. needs to be handled. 
                // Get it in order book stock info somehow
                "&m_ProductType=C" +
                "&m_ordFlow=" + IciciConstants.OrderDirectionString[(int)orderInfo.Direction] + // order direction S/B
                "&m_exchCd=" + IciciConstants.ExchangeString[(int)orderInfo.Exchange] + // get exchange
                "";

            string orderBookData = IciciGetWebPageResponse(URL_ICICI_EQT_CANCEL_ORDER,
                postData,
                URL_ICICI_REFERRER,
                mCookieContainer,
                out errorCode);

            if (errorCode.Equals(BrokerErrorCode.Success))
            {
                string cancelConfirm = "Your order has been cancelled";
                string cancelRequestSentToExchange = "Your request for order cancellation has been sent to the exchange";

                // Either cancel confirm or cancellation request sent to exchange both are succes cases
                // Success
                if (orderBookData.Contains(cancelConfirm) || orderBookData.Contains(cancelRequestSentToExchange))
                {
                    // TODO: revist if any prob
                    // Delete the orderRef from mEquityOrderBook
                    // We ideally want it to remain in equity order book in cancelled state
                    // but right now remove it rather than update
                    // because let the GetEquityOrddrBook update it as per its protocol
                    lock (lockObjectEquity)
                    {
                        mEquityOrderBook.Remove(orderRef);
                    }
                    return BrokerErrorCode.Success;
                }
            }

            return BrokerErrorCode.OrderCancelFailed;
        }

        // IBroker.CancelAllOutstandingEquityOrders
        // Stock-specific Cancellation of all outstanding orders
        public BrokerErrorCode CancelAllOutstandingEquityOrders(
            string stockCode,
            out Dictionary<string, BrokerErrorCode> cancelOrderErrCodes)
        {
            BrokerErrorCode errorCode = BrokerErrorCode.Success;

            cancelOrderErrCodes = new Dictionary<string, BrokerErrorCode>();
            Dictionary<string, EquityOrderBookRecord> stockOrdersToCancel;

            // Make sure we have the latest order book data filtered by stockcode
            errorCode = GetEquityOrderBookToday(false, true, stockCode, out stockOrdersToCancel);
            if (errorCode != BrokerErrorCode.Success)
            {
                return errorCode;
            }

            foreach (KeyValuePair<string, EquityOrderBookRecord> orderPair in stockOrdersToCancel)
            {
                // Try to cancel only cancellable orders
                if (orderPair.Value.Status == OrderStatus.ORDERED ||
                    orderPair.Value.Status == OrderStatus.PARTEXEC ||
                    orderPair.Value.Status == OrderStatus.REQUESTED)
                {
                    // Trim it
                    string orderRef = orderPair.Key.Trim(' ', '\t', '\r', '\n');

                    BrokerErrorCode tmpErrCode = CancelEquityOrder(orderRef, false);

                    if (tmpErrCode != BrokerErrorCode.Success)
                    {
                        errorCode = tmpErrCode;
                    }

                    cancelOrderErrCodes[orderRef] = tmpErrCode;
                }
            }

            return errorCode;

        }

        private BrokerErrorCode RefreshEquityOrderBookToday()
        {
            DateTime EarliestValidMarketOpenDate = MarketUtils.GetMarketCurrentDate();
            Dictionary<string, EquityOrderBookRecord> orders;
            return GetEquityOrderBookToday(false, false, null, out orders);
        }

        // IBroker.GetEquityTradeBook

        // Get trade book for a date range and for specific stock if stockCode is valid 
        // else gets complete trade book fro the date range
        public BrokerErrorCode GetEquityTradeBookToday(bool newTradesOnly,
            string stockCode,
            out Dictionary<string, EquityTradeBookRecord> trades)
        {
            trades = new Dictionary<string, EquityTradeBookRecord>();
            // Login If needed
            BrokerErrorCode errorCode = CheckAndLogInIfNeeded(false);
            if (errorCode != BrokerErrorCode.Success)
                return errorCode;

            string postData = "pgname=eqTrdBook&ismethodcall=0&mthname=";

            string tradeBookData = IciciGetWebPageResponse(URL_ICICI_EQT_TRADEBOOK,
                postData,
                URL_ICICI_REFERRER,
                mCookieContainer,
                out errorCode);

            if (errorCode.Equals(BrokerErrorCode.Success) && !tradeBookData.Contains("No matching Trades"))
            {
                // Trim it
                tradeBookData = tradeBookData.Trim(' ', '\t', '\r', '\n');

                tradeBookData = HtmlUtilities.EnsureHtmlParsable(tradeBookData);

                string subTradeBookData = StringParser.GetStringBetween(tradeBookData,
                    0,
                    "<thead>",
                    "</table>",
                    new string[] { });

                ParsedTable table = (ParsedTable)HtmlTableParser.ParseHtmlIntoTables("<table>" + subTradeBookData + "</table>", true);

                //trades = new Dictionary<string, EquityTradeBookRecord>();

                for (int i = 1; i < table.RowCount - 1; i++)
                {
                    EquityTradeBookRecord info = new EquityTradeBookRecord();
                    info.StockCode = table[i, 1].ToString().Trim();
                    // If stockCode parameter is not null i.e. all stocks are intended
                    // and if stock specific orders are intended and current element's stockCode doesnt match then iterate to next element
                    if (!string.IsNullOrEmpty(stockCode) && !info.StockCode.Equals(stockCode, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var temp = StringParser.GetStringBetween(table[i, 0].ToString(), 0, "nowrap;\">", "<", null);
                    DateTime.TryParse(temp, out info.Date);

                    //if (bSuccess)
                    //{
                    //    info[0].UpdateTime = result;
                    //}
                    string tempStr = table[i, 2].ToString().ToUpperInvariant().StartsWith("B") ? "BUY" : "SELL";
                    info.Direction = (OrderDirection)Enum.Parse(typeof(OrderDirection), tempStr);
                    info.Quantity = int.Parse(table[i, 3].ToString());
                    info.Price = double.Parse(table[i, 4].ToString());
                    info.TradeValue = double.Parse(table[i, 5].ToString());
                    //tempStr = StringParser.GetStringBetween(table[i, 6].ToString(), 0, "\">", "</a>", null);
                    info.Brokerage = double.Parse(table[i, 6].ToString());
                    info.SettlementNumber = StringParser.GetStringBetween(table[i, 7].ToString(), 0, "setno=", "&", null);
                    string orderRefString = StringParser.GetStringBetween(table[i, 10].ToString(), 0, "FML_ORD_ORDR_RFRNC=", "&", null);
                    info.OrderRefenceNumber = orderRefString.Trim();
                    lock (lockObjectEquity)
                    {
                        if (mEquityTradeBook.ContainsKey(info.OrderRefenceNumber))
                        {
                            if (newTradesOnly)
                            { }
                            else
                            {
                                trades.Add(info.OrderRefenceNumber, info);
                            }
                            // Update the trade
                            // update required because PartExec may have become full exec
                            mEquityTradeBook[info.OrderRefenceNumber] = info;
                        }
                        else
                        {
                            mEquityTradeBook.Add(info.OrderRefenceNumber, info);
                            trades.Add(info.OrderRefenceNumber, info);
                        }
                    }
                }
            }
            return errorCode;
        }

        // IBroker.GetEquityOrderBook
        public BrokerErrorCode GetEquityOrderBookToday(bool newOrdersOnly,
            bool bOnlyOutstandingOrders,
            string stockCode,
            out Dictionary<string, EquityOrderBookRecord> orders)
        {
            orders = new Dictionary<string, EquityOrderBookRecord>();
            // Login If needed
            BrokerErrorCode errorCode = CheckAndLogInIfNeeded(false);
            if (errorCode != BrokerErrorCode.Success)
                return errorCode;

            string postData = "pgname=eqOrdBook&ismethodcall=0&mthname=";

            string orderBookData = IciciGetWebPageResponse(URL_ICICI_EQT_ORDERBOOK,
                postData,
                URL_ICICI_REFERRER,
                mCookieContainer,
                out errorCode);

            if (errorCode.Equals(BrokerErrorCode.Success) && !orderBookData.Contains("No matching Record"))
            {
                // Trim it
                orderBookData = orderBookData.Trim(' ', '\t', '\r', '\n');

                orderBookData = HtmlUtilities.EnsureHtmlParsable(orderBookData);

                string subOrderBookData = StringParser.GetStringBetween(orderBookData,
                    0,
                    "<thead>",
                    "</table>",
                    new string[] { });

                ParsedTable table = (ParsedTable)HtmlTableParser.ParseHtmlIntoTables("<table>" + subOrderBookData + "</table>", true);

                //orders = new Dictionary<string, EquityOrderBookRecord>();
                for (int i = 1; i < table.RowCount; i++)
                {
                    EquityOrderBookRecord info = new EquityOrderBookRecord();
                    info.StockCode = table[i, 3].ToString().Trim();
                    info.StockCode = StringParser.GetStringBetween(info.StockCode, 0, "GetQuote('", "'", null);
                    // If stockCode parameter is empty/null i.e. all stocks are intended
                    // and if stock specific orders are intended and current element's stockCode doesnt match then iterate to next element
                    if (!string.IsNullOrEmpty(stockCode) && !info.StockCode.Equals(stockCode, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var temp = StringParser.GetStringBetween(table[i, 0].ToString(), 0, "nowrap;\">", "<", null);
                    DateTime.TryParse(temp, out info.Date);

                    string orderRefString = table[i, 14].ToString();
                    orderRefString = StringParser.GetStringBetween(orderRefString, 0, "FML_ORD_ORDR_RFRNC=", "&", null);
                    // Trim it
                    orderRefString = orderRefString.Trim();//(' ', '\t', '\r', '\n');

                    info.OrderRefenceNumber = orderRefString;
                    string tempStr = table[i, 4].ToString().ToUpperInvariant();
                    info.Direction = (OrderDirection)Enum.Parse(typeof(OrderDirection), tempStr);
                    info.Quantity = int.Parse(table[i, 5].ToString());

                    string price = StringParser.GetStringBetween(table[i, 3].ToString(),
                        0,
                        ">",
                        "<",
                        new string[] { "onclick", "font" });

                    /*
                    string price = StringParser.GetStringBetween(table[i, 3].ToString(),
                        0,
                        "<font color=\"blue\">",
                        "</font>",
                        new string[] { });

                    if (String.IsNullOrEmpty(price))
                    {
                        price = StringParser.GetStringBetween(table[i, 6].ToString(),
                        0,
                        "<font color=\"black\">",
                        "</font>",
                        new string[] { });
                    }

                    if (String.IsNullOrEmpty(price))
                    {
                        price = StringParser.GetStringBetween(table[i, 6].ToString(),
                        0,
                        "\">",
                        "</a>",
                        new string[] { "onclick"});
                    }
                    */

                    info.Price = double.Parse(price);

                    tempStr = table[i, 6].ToString().ToUpperInvariant();


                    // Executed orders have different string format, so ignore the clutter
                    if (tempStr.Contains("EXECUTED"))
                    {
                        tempStr = "EXECUTED";
                    }
                    else
                        tempStr = tempStr.Remove(tempStr.IndexOf("&"));

                    info.Status = (OrderStatus)Enum.Parse(typeof(OrderStatus), tempStr);

                    info.OpenQty = int.Parse(table[i, 8].ToString());
                    info.ExecutedQty = int.Parse(table[i, 9].ToString());

                    // Only add valid outstanding orders if bOnlyOutstandingOrders is true
                    // PARTEXEC is considered OUTSTANDING (i.e. not considered EXEC until fully executed)
                    if (!bOnlyOutstandingOrders ||
                        info.Status == OrderStatus.PARTEXEC ||
                        info.Status == OrderStatus.QUEUED ||
                        info.Status == OrderStatus.REQUESTED ||
                        info.Status == OrderStatus.ORDERED)
                    {
                        lock (lockObjectEquity)
                        {
                            if (mEquityOrderBook.ContainsKey(orderRefString))
                            {
                                if (newOrdersOnly)
                                { }
                                else
                                {
                                    orders.Add(orderRefString, info);
                                }
                                // Update the order
                                mEquityOrderBook[orderRefString] = info;
                            }
                            else
                            {
                                mEquityOrderBook.Add(orderRefString, info);
                                orders.Add(orderRefString, info);
                            }
                        }
                    }
                }
            }
            return errorCode;
        }

        private BrokerErrorCode GetEQTOrderPlacementCode(string orderResponse)
        {
            BrokerErrorCode code = BrokerErrorCode.Unknown;

            if (null == orderResponse)
            {
                return BrokerErrorCode.NullResponse;
            }
            if (orderResponse.IndexOf("Order placed successfully", StringComparison.OrdinalIgnoreCase) > 0)
            {
                return BrokerErrorCode.Success;
            }
            if (orderResponse.IndexOf("SQUARE OFF ORDER ACKNOWLEDGEMENT", StringComparison.OrdinalIgnoreCase) > 0)
            {
                return BrokerErrorCode.Success;
            }
            if (orderResponse.IndexOf("You have successfully converted the specified quantity to delivery", StringComparison.OrdinalIgnoreCase) > 0)
            {
                return BrokerErrorCode.Success;
            }
            //Client square off option is temporarily not available for this stock:Please select another stock.
            if (orderResponse.IndexOf("Please select another stock", StringComparison.OrdinalIgnoreCase) > 0)
            {
                return BrokerErrorCode.ContractNotEnabled;
            }
            if (orderResponse.IndexOf("Margin trading temporarily suspended", StringComparison.OrdinalIgnoreCase) > 0)
            {
                return BrokerErrorCode.ResourceNotAvailable;
            }
            if (orderResponse.IndexOf("Resource not available", StringComparison.OrdinalIgnoreCase) > 0)
            {
                return BrokerErrorCode.ResourceNotAvailable;
            }
            if (orderResponse.IndexOf("not enabled for trading", StringComparison.OrdinalIgnoreCase) > 0)
            {
                return BrokerErrorCode.ContractNotEnabled;
            }
            if (orderResponse.IndexOf("for the stock not available", StringComparison.OrdinalIgnoreCase) > 0)
            {
                return BrokerErrorCode.ContractNotEnabled;
            }
            if (orderResponse.IndexOf("Immediate or Cancel Orders can be placed", StringComparison.OrdinalIgnoreCase) > 0)
            {
                return BrokerErrorCode.ExchangeClosed;
            }
            if (orderResponse.IndexOf("have faced some technical or connectivity", StringComparison.OrdinalIgnoreCase) > -1)
            {
                return BrokerErrorCode.TechnicalReason;
            }
            if (orderResponse.IndexOf("close your browser and try again", StringComparison.OrdinalIgnoreCase) > -1)
            {
                return BrokerErrorCode.TechnicalReason;
            }
            if (orderResponse.IndexOf("insufficient to cover the trade value", StringComparison.OrdinalIgnoreCase) > -1)
            {
                return BrokerErrorCode.InsufficientLimit;
            }
            if (orderResponse.IndexOf("Multiples of Contract Lot size", StringComparison.OrdinalIgnoreCase) > -1)
            {
                return BrokerErrorCode.InvalidLotSize;
            }
            if (orderResponse.IndexOf("beyond the price range permitted by exchange", StringComparison.OrdinalIgnoreCase) > -1)
            {
                return BrokerErrorCode.OutsidePriceRange;
            }
            if (orderResponse.IndexOf("expiry is underway", StringComparison.OrdinalIgnoreCase) > -1)
            {
                return BrokerErrorCode.ExchangeClosed;
            }
            if (orderResponse.IndexOf("server error", StringComparison.OrdinalIgnoreCase) > 0)
            {
                return BrokerErrorCode.ServerError;
            }
            if (orderResponse.IndexOf("order not allowed when exchange is closed", StringComparison.OrdinalIgnoreCase) > 0)
            {
                return BrokerErrorCode.ExchangeClosed;
            }
            if (orderResponse.IndexOf("outside price range", StringComparison.OrdinalIgnoreCase) > 0)
            {
                return BrokerErrorCode.OutsidePriceRange;
            }
            if (orderResponse.IndexOf("nsufficient stock", StringComparison.OrdinalIgnoreCase) > 0)
            {
                return BrokerErrorCode.InsufficientStock;
            }
            if (orderResponse.IndexOf("nsufficient limit", StringComparison.OrdinalIgnoreCase) > 0)
            {
                return BrokerErrorCode.InsufficientLimit;
            }
            if (orderResponse.IndexOf("Invalid User", StringComparison.OrdinalIgnoreCase) > 0)
            {
                return BrokerErrorCode.NotLoggedIn;
            }
            if (orderResponse.IndexOf("The Login Id entered is not valid", StringComparison.OrdinalIgnoreCase) > 0)
            {
                return BrokerErrorCode.NotLoggedIn;
            }
            if (orderResponse.IndexOf("Invalid Login Id", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return BrokerErrorCode.NotLoggedIn;
            }
            if (orderResponse.IndexOf("Session has Timed Out", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return BrokerErrorCode.NotLoggedIn;
            }
            if (orderResponse.IndexOf("Please Login again", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return BrokerErrorCode.NotLoggedIn;
            }
            if (orderResponse.Length < 2000 && orderResponse.IndexOf("location.href=chttps + \"/customer/logon.asp\"", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return BrokerErrorCode.NotLoggedIn;
            }
            if (orderResponse.IndexOf("Check stock code", StringComparison.OrdinalIgnoreCase) > 0)
            {
                return BrokerErrorCode.InValidStockCode;
            }
            if (orderResponse.IndexOf("Stock may not be traded on the exchange selected or stock code may not be valid", StringComparison.OrdinalIgnoreCase) > 0)
            {
                return BrokerErrorCode.InValidStockCode;
            }
            return BrokerErrorCode.Unknown;
        }

        private string ExtractOrderReferenceNumber(string orderPageResponseData)
        {
            if (string.IsNullOrEmpty(orderPageResponseData))
                return null;

            // Order placed successfully response
            // go till Order Reference No
            // then find str between <td class="Lefttext"> 20170403N900015445  </ td >

            var index = orderPageResponseData.IndexOf("Order Reference No");
            var orderRef = HtmlParsingLibrary.StringParser.GetStringBetween(orderPageResponseData, index, "<td class=\"Lefttext\">", "</", null);

            if (!string.IsNullOrEmpty(orderRef))
                orderRef = orderRef.Trim();

            return orderRef;
        }

        private string ExtractOrderReferenceNumberMarginSqOffOrder(string orderPageResponseData)
        {
            if (string.IsNullOrEmpty(orderPageResponseData))
                return null;

            // Order placed successfully response
            // go till Your Reference No.
            // then find str between  <td class="Lefttext" colspan="2"> 20170407N300000486 </ td >

            var index = orderPageResponseData.IndexOf("Your Reference No.");
            var orderRef = HtmlParsingLibrary.StringParser.GetStringBetween(orderPageResponseData, index, "<td class=\"Lefttext\" colspan=\"2\">", "</", null);

            if (!string.IsNullOrEmpty(orderRef))
                orderRef = orderRef.Trim();

            return orderRef;
        }
    }
}
