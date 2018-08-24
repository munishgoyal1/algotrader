using System;
using System.Collections.Generic;
using System.IO;
using HtmlParsingLibrary;
using HttpLibrary;
using StockTrader.Core;
using StockTrader.Platform.Logging;
using StockTrader.Utilities;
using StockTrader.Utilities.Broker;

namespace StockTrader.Broker.IciciDirect
{
    public partial class IciciDirectBroker
    {
        private BrokerErrorCode GetFNOOrderPlacementCode(string orderResponse)
        {
            BrokerErrorCode code = BrokerErrorCode.Unknown;

            //isMarketOrder

            if (orderResponse.IndexOf("Resource not available", StringComparison.OrdinalIgnoreCase) > -1)
                code = BrokerErrorCode.ResourceNotAvailable;
            else if (orderResponse.IndexOf("not enabled for trading", StringComparison.OrdinalIgnoreCase) > -1)
                code = BrokerErrorCode.ContractNotEnabled;
            else if (orderResponse.IndexOf("for the stock not available", StringComparison.OrdinalIgnoreCase) > -1)
                code = BrokerErrorCode.ContractNotEnabled;
            else if (orderResponse.IndexOf("Immediate or Cancel Orders can be placed", StringComparison.OrdinalIgnoreCase) > -1)
                code = BrokerErrorCode.ExchangeClosed;
            else if (orderResponse.IndexOf("have faced some technical or connectivity", StringComparison.OrdinalIgnoreCase) > -1)
                code = BrokerErrorCode.TechnicalReason;
            else if (orderResponse.IndexOf("close your browser and try again", StringComparison.OrdinalIgnoreCase) > -1)
                code = BrokerErrorCode.TechnicalReason;
            else if (orderResponse.IndexOf("insufficient to cover the trade value", StringComparison.OrdinalIgnoreCase) > -1)
                code = BrokerErrorCode.InsufficientLimit;
            else if (orderResponse.IndexOf("Multiples of Contract Lot size", StringComparison.OrdinalIgnoreCase) > -1)
                code = BrokerErrorCode.InvalidLotSize;
            else if (orderResponse.IndexOf("Limit rate should be Zero for Market orders", StringComparison.OrdinalIgnoreCase) > -1)
                code = BrokerErrorCode.InValidArg;
            else if (orderResponse.IndexOf("beyond the price range permitted by exchange", StringComparison.OrdinalIgnoreCase) > -1)
                code = BrokerErrorCode.OutsidePriceRange;
            else if (orderResponse.IndexOf("expiry is underway", StringComparison.OrdinalIgnoreCase) > -1)
                code = BrokerErrorCode.ExchangeClosed;

            else if (orderResponse.IndexOf("Invalid User", StringComparison.OrdinalIgnoreCase) > 0)
                code = BrokerErrorCode.NotLoggedIn;
            else if (orderResponse.IndexOf("The Login Id entered is not valid", StringComparison.OrdinalIgnoreCase) > 0)
                code = BrokerErrorCode.NotLoggedIn;
            else if (orderResponse.IndexOf("Invalid Login Id", StringComparison.OrdinalIgnoreCase) >= 0)
                code = BrokerErrorCode.NotLoggedIn;
            else if (orderResponse.IndexOf("Session has Timed Out", StringComparison.OrdinalIgnoreCase) >= 0)
                code = BrokerErrorCode.NotLoggedIn;
            else if (orderResponse.IndexOf("Please Login again", StringComparison.OrdinalIgnoreCase) >= 0)
                code = BrokerErrorCode.NotLoggedIn;

            else code = BrokerErrorCode.Unknown;

            return code;
        }
        public BrokerErrorCode PlaceDerivativeOrder(string stockCode,
            int quantity,
            double price,
            OrderPriceType orderType,
            OrderDirection orderDirection,
            InstrumentType instrumentType,
            double strikePrice,
            DateTime expiryDate,
            OrderGoodNessType orderGoodnessType,
            out string orderNumber)
        {
            orderNumber = null;

            // Login If needed
            BrokerErrorCode code = CheckAndLogInIfNeeded(false);
            if (code != BrokerErrorCode.Success)
                return code;


            string strExpDate = expiryDate.ToString("dd-MMM-yyyy");
            string contract = stockCode + "-" + strExpDate;
            bool isFuture = false;
            bool isIndex = false;
            bool isCall = instrumentType == InstrumentType.OptionCallIndex || instrumentType == InstrumentType.OptionCallStock;

            if (instrumentType == InstrumentType.FutureIndex || instrumentType == InstrumentType.FutureStock)
            {
                isFuture = true;
                contract = "FUT-" + contract;
            }
            else if (instrumentType != InstrumentType.Share)
                contract = string.Format("{0}-{1}-{2}-{3}", "OPT", contract, (int)strikePrice, isCall ? "CE" : "PE");
            else
                throw new NotSupportedException();

            if (instrumentType == InstrumentType.FutureIndex || instrumentType == InstrumentType.OptionCallIndex || instrumentType == InstrumentType.OptionPutIndex)
                isIndex = true;

            string orderGoodTill = orderGoodnessType.Equals(OrderGoodNessType.IOC) ? "I" : "T";

            FileTracing.TraceOut(string.Format("About to PlaceDerivativeOrder. Contract: {0}, Side: {1}, Price: {2}, Qty: {3}", contract, orderDirection, price, quantity));

            string placeOrderQuery = "m_PipeId=" + m_PipeId_value + "&" +
                "FFO_ORD_TRD_DT=" + FFO_ORD_TRD_DT_value + "+++++++++++&" +
                "FFO_ORDR_FLW=" + (orderDirection == OrderDirection.BUY ? "B" : "S") + "&" +
                "FFO_PRDCT_TYP=" + (isFuture ? "F" : "O") + "&" +
                "FFO_OPT_TYP=" + (isCall ? "C&" : "P&") +
                "FFO_UNDRLYNG=" + stockCode + "&" +
                "FFO_EXPRY_DT=" + strExpDate + "&" +
                "FFO_EXER_TYP=E&" +
                "FFO_RQST_TYP=F&" + //(isFuture ? "U&" : "F&") +
                "FFO_STRK_PRC=" + (int)(strikePrice * 100) + "&" +
                "FFO_CTGRY_INDSTK=" + (isIndex ? "I" : "S") + "&" +
                //FFO_MIN_LOT_QTY=20
                //FFO_LST_TRD_PRC=529405
                "FFO_CLNT_CTGRY=O&" +
                "FFO_TRD_PSWD_LMT=10000000&" +
                //FFO_OFLN_STTS=C
                //FFO_CRRNT_STTS0=C
                "FFO_XCHNG_ST=C&" +
                "FFO_ACCOUNT=" + FML_ACCOUNT_value + "&" +
                "FFO_EBA_MTCH_ACT_NO=" + ACCOUNTNUM + "&" +
                "FFO_CLNT_DPID=" + CLIENTID + "&" +
                "FFO_DPID=" + DPID + "&" +
                //FFO_BNK_ACT_NO=
                "FFO_XCHNG_CD=NFO&" +
                "FFO_QTY=" + quantity.ToString() + "&" +
                //FFO_QTY_Hidden=20
                "FFO_ORD_TYP=" + (orderType == OrderPriceType.LIMIT ? "L" : "M") + "&" +
                "FFO_LMT_RT=" + price + "&" +
                "FFO_GOOD_TILL_TYPE=" + orderGoodTill + "&" + //I==>IOC, T==>Day
                "FFO_GOOD_TILL_DATE_TEXT=" + FFO_GOOD_TILL_DATE_value + "&" +
                "FFO_GOOD_TILL_DATE=" + FFO_GOOD_TILL_DATE_value + "&" +
                "FFO_GOOD_TILL_DATE1=" + FFO_GOOD_TILL_DATE_value + "&" +
                //FFO_GOOD_TILL_BACK=
                "FFO_GOOD_TILL_DATE_TEMP=" + FFO_GOOD_TILL_DATE_TEMP_value + "&" +
                "date2=" + date2_value + "&" +
                //dateEx=3/29/2012
                "date7=" + date7_value + "&" +
                "Submit=Submit&" +
                //FFO_STP_LSS=
                "Submit2=Clear&" +
                "";

            string preResponse = HttpHelper.GetWebPageResponse(URL_ICICI_FNO_VERIFYORDER,
              placeOrderQuery,
              URL_ICICI_FNO_HOME,
              mCookieContainer);

            if (preResponse.IndexOf("ORDER VERIFICATION") == -1)
                return GetFNOOrderPlacementCode(preResponse); ;

            //Quote for the stock not available:Quote unavailable for the stock

            string verifyOrderQuery =
                "FFO_ORD_TRD_DT=" + FFO_ORD_TRD_DT_value + "+++++++++++&" +
                "FFO_XCHNG_ST=C&" +
                "FFO_QTY=" + quantity.ToString() + "&" +
                "FFO_UNDRLYNG=" + stockCode + "&" +
                "FFO_PRDCT_TYP=" + (isFuture ? "F" : "O") + "&" +
                "FFO_ORD_TYP=" + (orderType == OrderPriceType.LIMIT ? "L" : "M") + "&" +
                "FFO_ACCOUNT=" + FML_ACCOUNT_value + "&" +
                "FFO_XCHNG_CD=NFO&" +
                "FFO_CTGRY_INDSTK=" + (isIndex ? "I" : "S") + "&" +
                "FFO_LMT_RT=" + price + "&" +
                //FFO_DSCLSD_QTY=
                //FFO_STP_LSS=
                "FFO_ORDR_FLW=" + (orderDirection == OrderDirection.BUY ? "B" : "S") + "&" +
                "FFO_STRK_PRC=" + (int)(strikePrice * 100) + "&" +
                "FFO_EXPRY_DT=" + strExpDate + "&" +
                "FFO_EXER_TYP=E&" +
                "FFO_OPT_TYP=" + (isCall ? "C&" : "P&") +
                "FFO_PASS=1&" +
                //FFO_RT=
                //FFO_SESSION_ID=
                "FFO_EBA_MTCH_ACT_NO=" + ACCOUNTNUM + "&" +
                "FFO_DPID=" + DPID + "&" +
                "FFO_CLNT_DPID=" + CLIENTID + "&" +
                "FFO_CLNT_CTGRY=O&" +
                //FFO_BNK_ACT_NO=
                "FFO_RQST_TYP=F&" +
                "FFO_TRD_PSWD_LMT=10000000&" +
                //FFO_MIN_LOT_QTY=20
                "FFO_GOOD_TILL_DATE=" + FFO_GOOD_TILL_DATE_value + "&" +
                "FFO_GOOD_TILL_TYPE=" + orderGoodTill + "&" + //I==>IOC, T==>Day
                "FFO_GOOD_TILL_DATE_TEXT=" + FFO_GOOD_TILL_DATE_value + "&" +
                "FFO_GOOD_TILL_DATE_TEMP=" + FFO_GOOD_TILL_DATE_TEMP_value + "&" +
                //FFO_TRD_FLG=
                //FFO_MULTI_ROW=
                //FFO_LST_TRD_PRC=529405
                //FFO_ALIAS=
                "m_PipeId=" + m_PipeId_value + "&" +
                "smt=Proceed&" +
                "Submit2=Back&" +
                "";

            string verifyOrderResponse = HttpHelper.GetWebPageResponse(URL_ICICI_FNO_VERIFYORDER,
                verifyOrderQuery,
                URL_ICICI_FNO_VERIFYORDER,
                mCookieContainer);

            if (verifyOrderResponse.IndexOf("Order Acknowledgement") > -1 ||
                verifyOrderResponse.IndexOf("Your Order will be sent to") > -1)
            {
                orderNumber = StringParser.GetStringBetween(verifyOrderResponse,
                    0,
                    "width=\"50%\">",
                    "</td>",
                    new string[] { "Your Reference" });
                code = BrokerErrorCode.Success;
                FileTracing.TraceOut(string.Format("PlaceDerivativeOrder Success. Contract: {0}, Side: {1}, Price: {2}, Qty: {3}", contract, orderDirection, price, quantity));
            }
            else code = GetFNOOrderPlacementCode(verifyOrderResponse);

            if (code != BrokerErrorCode.Success)
            {
                string desc = string.Format("PlaceDerivativeOrder Failed-{0} {1} qty {2} at {3}", stockCode, orderDirection, quantity, price);
                string content = string.Format("{0}\n{1}\n\n\n{2}\n\n\n{3}", desc, code, preResponse, verifyOrderResponse);
                Logger.LogErrorText(desc, content);
            }

            return code;
        }

        public BrokerErrorCode PlaceDerivativeFBSOrder(string stockCode,
           int quantity,
           double price,
           OrderPriceType orderPriceType,
           OrderDirection orderDirection,
           InstrumentType instrumentType,
           int minLotSize,
           double strikePrice,
           OrderGoodNessType orderGoodnessType,
           DateTime expiryDate)
        {
            // Login If needed
            BrokerErrorCode code = CheckAndLogInIfNeeded(false);
            if (code != BrokerErrorCode.Success)
                return code;

            string strExpDate = expiryDate.ToString("dd-MMM-yyyy");
            string contract = stockCode + "-" + strExpDate;
            bool isFuture = false;
            bool isIndex = false;
            bool isCall = instrumentType == InstrumentType.OptionCallIndex || instrumentType == InstrumentType.OptionCallStock;

            if (instrumentType == InstrumentType.FutureIndex || instrumentType == InstrumentType.FutureStock)
            {
                isFuture = true;
                contract = "FUT-" + contract;
            }
            else if (instrumentType != InstrumentType.Share)
                contract = string.Format("{0}-{1}-{2}-{3}", "OPT", contract, (int)strikePrice, isCall ? "CE" : "PE");
            else
                throw new NotSupportedException();

            if (instrumentType == InstrumentType.FutureIndex || instrumentType == InstrumentType.OptionCallIndex || instrumentType == InstrumentType.OptionPutIndex)
                isIndex = true;

            string orderGoodTill = orderGoodnessType.Equals(OrderGoodNessType.IOC) ? "I" : "T";

            string orderDateVal = FFO_ORD_TRD_DT_value.Remove(FFO_ORD_TRD_DT_value.IndexOf('+'));

            FileTracing.TraceOut("PlaceFutureOrderFBS: " + contract);

            string query =
                "FFO_CRRNT_STTS0=O" +
                "&FFO_ACCOUNT=" + FML_ACCOUNT_value +
                "&FFO_EBA_MTCH_ACT_NO=" + ACCOUNTNUM +
                "&FFO_CLNT_DPID=" + CLIENTID +
                "&FFO_DPID=" + DPID +
                "&FFO_BNK_ACT_NO=" +
                "&FFO_ORDR_FLW=" + (orderDirection == OrderDirection.BUY ? "B" : "S") +
                "&FFO_XCHNG_CD=NFO" +
                "&FFO_PRDCT_TYP=" + (isFuture ? "F" : "O") +
                "&FFO_OPT_TYP=" + (isCall ? "C" : "P") +
                "&FFO_UNDRLYNG=" + stockCode +
                "&FFO_CONTRACT=" + contract +
                "&FFO_CONTRACT_HIDDEN=" + contract +
                "&FFO_QTY=" + quantity.ToString() +
                "&FFO_QTY_Hidden=" + quantity.ToString() +
                string.Format("&One_Lot_Size:(1 Lot = {0} Quantity)", minLotSize.ToString()) +
                "&FFO_ORD_TYP=" + (orderPriceType == OrderPriceType.LIMIT ? "L" : "M") +
                "&FFO_ORD_TYP_HIDDEN=" + (orderPriceType == OrderPriceType.LIMIT ? "L" : "M") +
                "&FFO_LMT_RT=" + price + "&" +
                "&FFO_GOOD_TILL_TYPE=" + orderGoodTill + //I==>IOC, T==>Day
                "&FFO_GOOD_TILL_DATE_TEXT=" + FFO_GOOD_TILL_DATE_value +
                "&FFO_GOOD_TILL_DATE=" + FFO_GOOD_TILL_DATE_value +
                "&FFO_GOOD_TILL_DATE1=" + FFO_GOOD_TILL_DATE1_value +
                "&FFO_GOOD_TILL_BACK=" +
                "&FFO_GOOD_TILL_DATE_TEMP=" + FFO_GOOD_TILL_DATE_TEMP_value +
                "&date2=" + date2_value +
                "&dateEx=" +
                "&date7=" + date7_value +
                "&FFO_STP_LSS=&smt=Submit&FFO_TRADING_PASSWD=&Submit2=Clear" +
                "&m_PipeId=" + m_PipeId_value +
                "&FFO_ORD_TRD_DT=" + orderDateVal + //"+++++++++++&" +
                "&FFO_EXPRY_DT=" + strExpDate +
                "&FFO_EXER_TYP=E" +
                "&FFO_RQST_TYP=" + (isFuture ? "U" : "O") +
                "&FFO_STRK_PRC=" + (int)(strikePrice * 100) +
                "&FFO_CLNT_CTGRY=O" +
                "&FFO_TRD_PSWD_LMT=10000000" +
                "&FFO_CTGRY_INDSTK=" + (isIndex ? "I" : "S") +
                "&FFO_MIN_LOT_QTY=" + minLotSize.ToString() +
                "&FFO_LST_TRD_PRC=" +
                "&FFO_OFLN_STTS=C" +
                "&FFO_XCHNG_ST=O";

            string response = HttpHelper.GetWebPageResponse(URL_ICICI_FNO_FBS_SUBMITORDER,
                query,
                URL_ICICI_FNO_HOME,
                mCookieContainer);

            if (response.Length < 1000 && response.IndexOf("showTradingData('OrderBook');") > -1)
                code = BrokerErrorCode.Success;
            else code = GetFNOOrderPlacementCode(response);

            if (code != BrokerErrorCode.Success)
            {
                string desc = string.Format("PlaceDerivativeFBSOrder-{0}-{1}-{2}", stockCode, quantity, price);
                string content = string.Format("{0}\n{1}\n\n\n{2}", desc, code, response);
                Logger.LogErrorText(desc, content);
            }

            return code;
        }

        public BrokerErrorCode PlaceDerivativeFBSOrder1(string stockCode,
          int quantity,
          double price,
          OrderPriceType orderPriceType,
          OrderDirection orderDirection,
          InstrumentType instrumentType,
            double strikePrice,
           OrderGoodNessType orderGoodnessType,
          DateTime expiryDate)
        {
            // Login If needed
            BrokerErrorCode code = CheckAndLogInIfNeeded(false);
            if (code != BrokerErrorCode.Success)
                return code;

            string strExpDate = expiryDate.ToString("dd-MMM-yyyy");
            string contract = stockCode + "-" + strExpDate;
            bool isFuture = false;
            bool isIndex = false;
            bool isCall = instrumentType == InstrumentType.OptionCallIndex || instrumentType == InstrumentType.OptionCallStock;

            if (instrumentType == InstrumentType.FutureIndex || instrumentType == InstrumentType.FutureStock)
            {
                isFuture = true;
                contract = "FUT-" + contract;
            }
            else if (instrumentType != InstrumentType.Share)
                contract = string.Format("{0}-{1}-{2}-{3}", "OPT", contract, (int)strikePrice, isCall ? "CE" : "PE");
            else
                throw new NotSupportedException();

            if (instrumentType == InstrumentType.FutureIndex || instrumentType == InstrumentType.OptionCallIndex || instrumentType == InstrumentType.OptionPutIndex)
                isIndex = true;

            string orderGoodTill = orderGoodnessType.Equals(OrderGoodNessType.IOC) ? "I" : "T";

            string orderDateVal = FFO_ORD_TRD_DT_value.Remove(FFO_ORD_TRD_DT_value.IndexOf('+'));

            FileTracing.TraceOut("PlaceFutureOrder: " + contract);

            string query = "m_PipeId=" + m_PipeId_value + "&" +
                "FFO_ORD_TRD_DT=" + FFO_ORD_TRD_DT_value + //"+++++++++++&" +
                "&FFO_EXPRY_DT=" + strExpDate + "&" +
                "FFO_EXER_TYP=E&FFO_RQST_TYP=F&FFO_STRK_PRC=0&FFO_CLNT_CTGRY=O&FFO_TRD_PSWD_LMT=10000000&" +
                "FFO_CTGRY_INDSTK=" + (isIndex ? "I" : "S") + "&" +
                "FFO_MIN_LOT_QTY=50&FFO_OFLN_STTS=C&FFO_CRRNT_STTS0=C&FFO_XCHNG_ST=C&" +
                "FFO_ACCOUNT=" + FML_ACCOUNT_value + "&" +
                "FFO_EBA_MTCH_ACT_NO=" + ACCOUNTNUM + "&" +
                "FFO_CLNT_DPID=" + CLIENTID + "&" +
                "FFO_DPID=" + DPID + "&" +
                "FFO_ORDR_FLW=" + (orderDirection == OrderDirection.BUY ? "B" : "S") + "&" +
                "FFO_XCHNG_CD=NFO&" +
                "FFO_PRDCT_TYP=" + (isFuture ? "F" : "O") + "&" +
                "FFO_UNDRLYNG=" + stockCode + "&" +
                "FFO_OPT_TYP=" + (isCall ? "C" : "P") +
                "&FFO_CONTRACT_HIDDEN=" + contract + "&" +
                "FFO_QTY=" + quantity.ToString() + "&" +
                "FFO_ORD_TYP=" + (orderPriceType == OrderPriceType.LIMIT ? "L" : "M") + "&" +
                "FFO_ORD_TYP_HIDDEN=" + (orderPriceType == OrderPriceType.LIMIT ? "L" : "M") + "&" +
                "FFO_LMT_RT=" + price + "&" +
                //"FFO_GOOD_TILL_TYPE=" + orderGoodTill + //I==>IOC, T==>Day
                "&FFO_GOOD_TILL_DATE=" + FFO_GOOD_TILL_DATE_value + "&" +
                "FFO_GOOD_TILL_DATE1=" + FFO_GOOD_TILL_DATE1_value + "&" +
                "FFO_GOOD_TILL_DATE_TEMP=" + FFO_GOOD_TILL_DATE_TEMP_value + "&" +
                "date2=" + date2_value + "&" +
                "date7=" + date7_value + "&" +
                "FFO_STP_LSS=";

            string response = HttpHelper.GetWebPageResponse(URL_ICICI_FNO_FBS_SUBMITORDER,
                query,
                URL_ICICI_FNO_HOME,
                mCookieContainer);

            if (response.IndexOf("<title>ICICI direct.com :: F & O :: Order Book</title>") > -1)
                code = BrokerErrorCode.Success;
            else if (response.IndexOf("Immediate or Cancel Orders can be placed") > -1)
                code = BrokerErrorCode.ExchangeClosed;
            else if (response.IndexOf("have faced some technical or connectivity") > -1)
                code = BrokerErrorCode.TechnicalReason;
            else if (response.IndexOf("close your browser and try again") > -1)
                code = BrokerErrorCode.TechnicalReason;
            else if (response.IndexOf("insufficient to cover the trade value") > -1)
                code = BrokerErrorCode.InsufficientLimit;
            else if (response.IndexOf("Multiples of Contract Lot size") > -1)
                code = BrokerErrorCode.InvalidLotSize;
            else if (response.IndexOf("ICICI direct.com :: Login") > -1)
                code = BrokerErrorCode.NotLoggedIn;
            else if (response.IndexOf("Order outside price range") > -1)
                code = BrokerErrorCode.OutsidePriceRange;
            else code = BrokerErrorCode.Unknown;

            if (code != BrokerErrorCode.Success)
            {
                string desc = string.Format("PlaceDerivativeFBSOrder-{0}-{1}-{2}", stockCode, quantity, price);
                string content = string.Format("{0}\n{1}\n\n\n{2}", desc, code, response);
                Logger.LogErrorText(desc, content);
            }

            return code;
        }

        public BrokerErrorCode GetDerivativeQuote(string underlyingSymbol,
            InstrumentType instrumentType,
            DateTime expiryDate,
            double strikePrice,
            out DerivativeSymbolQuote info)
        {
            info = new DerivativeSymbolQuote();
            // Login If needed
            BrokerErrorCode errorCode = BrokerErrorCode.Success;
            //BrokerErrorCode errorCode = CheckAndLogInIfNeeded(false);
            //if (errorCode != BrokerErrorCode.Success)
            //{
            //    return errorCode;
            //}

            //FFO_UNDRLYNG     NIFTY        
            //FFO_XCHNG_CD     NFO          
            //FFO_MIN_LOT_QTY  50           
            //FFO_PRDCT_TYP    O            
            //FFO_OPT_TYP      P            
            //FFO_STRK_PRC     590000       
            //FFO_EXPRY_DT     28-Apr-2011  
            //FFO_EXER_TYP     E   

            if (instrumentType == InstrumentType.Share) throw new NotSupportedException();

            //if (instrumentType == InstrumentType.FutureIndex && instrumentType != InstrumentType.FutureStock) throw new NotSupportedException();

            bool isOptionType = StockUtils.IsInstrumentOptionType(instrumentType);
            string prdctType = StockUtils.GetInstrumentTypeForGetQuoteString(instrumentType);

            string postData =

                "FFO_PRDCT_TYP=" + prdctType +
                "&FFO_UNDRLYNG=" + underlyingSymbol +
                "&FFO_EXER_TYP=E" +
                "&FFO_EXPRY_DT=" + expiryDate.ToString("dd-MMM-yyyy") +
                "&FFO_XCHNG_CD=NFO";

            if (isOptionType)
            {
                postData += "&FFO_STRK_PRC=" + (int)strikePrice + "00";
                postData += "&FFO_OPT_TYP=" + StockUtils.GetFnOInstrumentGetQuoteDirectionString(instrumentType);
            }
            else
            {
                postData += "&FFO_CTGRY_INDSTK=" + ((instrumentType == InstrumentType.FutureStock) ? "S" : "I");
            }

            string quoteData = null;

            do
            {
                quoteData = IciciGetWebPageResponse(URL_ICICI_FNO_QUOTE,
                     postData,
                     null,
                     mCookieContainer,
                     out errorCode);
            } while (!LoginUtils.IsErrorFatal(errorCode) && quoteData == null);

            if (quoteData.Contains("No records to be displayed"))
            {
                return BrokerErrorCode.InValidContract;
            }
            if (quoteData.Contains("Resource not available"))
            {
                return BrokerErrorCode.ResourceNotAvailable;
            }

            if (!errorCode.Equals(BrokerErrorCode.Success))
            {
                return errorCode;
            }

            int insertionIdx = quoteData.IndexOf("<!--<input type=\"hidden\" name=\"FFO_PRDCT_TYP\" value=\"" + prdctType + "\">");
            quoteData = quoteData.Insert(insertionIdx, "</td></tr></table>");

            insertionIdx = quoteData.IndexOf("<td class=\"content\" align=\"left\" width=\"15%\" nowrap>");
            int removeIdx = quoteData.IndexOf("</td>", insertionIdx);
            quoteData = quoteData.Remove(removeIdx, 5);
            quoteData = quoteData.Insert(insertionIdx, "</td>");

            ParsedTable table = (ParsedTable)HtmlTableParser.ParseHtmlIntoTables(quoteData, true);

            info.ExpiryDate = expiryDate.ToString("ddMMMyyyy");
            info.InstrumentType = instrumentType;

            string tempStr = ParsedTable.GetValue(table, new int[] { 0, 3, 2, 2 });
            DateTime lastTradeTime = DateTime.Parse(tempStr);
            tempStr = ParsedTable.GetValue(table, new int[] { 0, 3, 2, 5 });
            lastTradeTime += TimeSpan.Parse(tempStr);
            info.UpdateTime = lastTradeTime;
            info.QuoteTime = lastTradeTime;

            info.LastTradedPriceDouble = double.Parse(ParsedTable.GetValue(table, new int[] { 0, 5, 0, 0, 0, 1 }));
            info.VolumeTradedInt = MarketUtils.GetVolume(ParsedTable.GetValue(table, new int[] { 0, 5, 0, 0, 6, 1 }));
            tempStr = ParsedTable.GetValue(table, new int[] { 0, 5, 0, 0, 0, 3 });
            tempStr = tempStr.Substring(0, tempStr.IndexOf("\r\n"));
            info.BestBidPriceDouble = double.Parse(tempStr);
            info.BestOfferPriceDouble = double.Parse(ParsedTable.GetValue(table, new int[] { 0, 5, 0, 0, 1, 3 }));
            info.BestBidQuantityInt = MarketUtils.GetVolume(ParsedTable.GetValue(table, new int[] { 0, 5, 0, 0, 2, 3 }));
            info.BestOfferQuantityInt = MarketUtils.GetVolume(ParsedTable.GetValue(table, new int[] { 0, 5, 0, 0, 3, 3 }));
            info.AssetPrice = double.Parse(ParsedTable.GetValue(table, new int[] { 0, 5, 0, 0, 6, 3 }));
            info.DayOpenPrice = double.Parse(ParsedTable.GetValue(table, new int[] { 0, 5, 0, 0, 1, 1 }));
            info.DayHighPrice = double.Parse(ParsedTable.GetValue(table, new int[] { 0, 5, 0, 0, 2, 1 }));
            info.DayLowPrice = double.Parse(ParsedTable.GetValue(table, new int[] { 0, 5, 0, 0, 3, 1 }));
            info.PrevClosePrice = double.Parse(ParsedTable.GetValue(table, new int[] { 0, 5, 0, 0, 4, 1 }));
            info.PercChangeFromPrevious = MarketUtils.GetPercentage(ParsedTable.GetValue(table, new int[] { 0, 5, 0, 0, 5, 1 }));
            info.StrikePriceDouble = double.Parse(strikePrice.ToString());
            info.UnderlyingSymbol = underlyingSymbol;
            info.UpdateTime = DateTime.Now;

            //MUNISHTODOREMOVE:
            // Insert to Database
            //if (errorCode == BrokerErrorCode.Success)
            //{
            //    DerivativeQuoteRecord dqr = new DerivativeQuoteRecord(info);
            //    dqr.Persist();
            //}

            return errorCode;
        }

        public BrokerErrorCode GetDerivativeSpread(string underlyingSymbol,
            InstrumentType instrumentType,
            DateTime expiryDate,
            double strikePrice,
            out DerivativeSymbolSpread info)
        {
            info = new DerivativeSymbolSpread();
            BrokerErrorCode errorCode = BrokerErrorCode.Success;
            // Login If needed
            //BrokerErrorCode errorCode = CheckAndLogInIfNeeded(false);
            //if (errorCode != BrokerErrorCode.Success)
            //{
            //    return errorCode;
            //}

            if (instrumentType == InstrumentType.Share) throw new NotSupportedException();

            bool isOptionType = StockUtils.IsInstrumentOptionType(instrumentType);
            string prdctType = StockUtils.GetInstrumentTypeForGetQuoteString(instrumentType);
            //?Page=&FFO_UNDRLYNG=NIFTY%20&FFO_XCHNG_CD=NFO&FFO_PRDCT_TYP=O&FFO_OPT_TYP=C&FFO_EXPRY_DT=26-Apr-2012&FFO_EXER_TYP=E
            //&FFO_RQST_TYP=*&FFO_STRK_PRC=560000&FFO_MIN_LOT_QTY=50
            //Page=&FFO_UNDERLYNG=RELIND%20&FFO_XCHNG_CD=NFO&FFO_PRDCT_TYP=O&FFO_OPT_TYP=C&FFO_EXPRY_DT=26-Apr-2012&FFO_EXER_TYP=E
            //&FFO_RQST_TYP=*&FFO_STRK_PRC=74000&FFO_MIN_LOT_QTY=250
            //Page=&FFO_UNDRLYNG=RELIND%20&FFO_XCHNG_CD=NFO&FFO_PRDCT_TYP=F&FFO_OPT_TYP=*&FFO_EXPRY_DT=26-Apr-2012&FFO_EXER_TYP=E
            //&FFO_RQST_TYP=*&FFO_STRK_PRC=undefined&FFO_MIN_LOT_QTY=250
            string postData =

                "FFO_PRDCT_TYP=" + prdctType +
                "&FFO_UNDRLYNG=" + underlyingSymbol +
                "&FFO_EXER_TYP=E" +
                "&FFO_EXPRY_DT=" + expiryDate.ToString("dd-MMM-yyyy") +
                "&FFO_XCHNG_CD=NFO" +
                "&FFO_MIN_LOT_QTY=";

            if (isOptionType)
            {
                postData += "&FFO_STRK_PRC=" + (int)strikePrice + "00";
                postData += "&FFO_OPT_TYP=" + StockUtils.GetFnOInstrumentGetQuoteDirectionString(instrumentType);
            }
            else
            {
                postData += "&FFO_CTGRY_INDSTK=" + ((instrumentType == InstrumentType.FutureStock) ? "S" : "I");
            }

            string quoteData = null;

            do
            {
                string url = URL_ICICI_FNO_SPREAD + "?" + postData;
                quoteData = IciciGetWebPageResponse(url,
                     null,//postData,
                     null,
                     mCookieContainer,
                     out errorCode);
            } while (!LoginUtils.IsErrorFatal(errorCode) && quoteData == null);

            if (quoteData.Contains("No records to be displayed"))
                return BrokerErrorCode.InValidContract;
            if (quoteData.Contains("Resource not available"))
                return BrokerErrorCode.ResourceNotAvailable;
            if (!errorCode.Equals(BrokerErrorCode.Success))
                return errorCode;

            ParsedTable table = (ParsedTable)HtmlTableParser.ParseHtmlIntoTables(quoteData, true);

            info.Exchange = Exchange.NSE;
            info.ExpiryDate = MarketUtils.GetExpiryDate(expiryDate.ToString("ddMMMyyyy"));
            info.InstrumentType = instrumentType;
            info.Symbol = underlyingSymbol;
            info.StrikePrice = strikePrice;

            string tempStr = ParsedTable.GetValue(table, new int[] { 0, 1, 1, 2 });
            DateTime lastTradeTime = DateTime.Parse(tempStr);
            tempStr = ParsedTable.GetValue(table, new int[] { 0, 1, 1, 5 });
            lastTradeTime += TimeSpan.Parse(tempStr);
            info.QuoteTime = lastTradeTime;

            info.TotalBidQty = MarketUtils.GetVolume(ParsedTable.GetValue(table, new int[] { 0, 3, 6, 1 }));
            info.TotalOfferQty = MarketUtils.GetVolume(ParsedTable.GetValue(table, new int[] { 0, 3, 6, 3 }));

            for (int i = 0; i < 5; i++)
            {
                info.BestBidPrice[i] = MarketUtils.GetPrice(ParsedTable.GetValue(table, new int[] { 0, 3, i + 1, 1 }));
                info.BestBidQty[i] = MarketUtils.GetVolume(ParsedTable.GetValue(table, new int[] { 0, 3, i + 1, 0 }));

                info.BestOfferPrice[i] = MarketUtils.GetPrice(ParsedTable.GetValue(table, new int[] { 0, 3, i + 1, 4 }));
                info.BestOfferQty[i] = MarketUtils.GetVolume(ParsedTable.GetValue(table, new int[] { 0, 3, i + 1, 3 }));
            }
            //MUNISHTODOREMOVE:
            // Insert to Database
            //if (errorCode == BrokerErrorCode.Success)
            //{
            //    DerivativeQuoteRecord dqr = new DerivativeQuoteRecord(info);
            //    dqr.Persist();
            //}

            return errorCode;
        }

        // It gets all type of orders. (instrumentType is ignored currently)
        public BrokerErrorCode GetDerivativesOrderBook(DateTime fromDate,
            DateTime toDate,
            InstrumentType instrumentType,
            bool newTradesOnly,
            bool bOnlyOutstandingOrders,
            out Dictionary<string, DerivativeOrderBookRecord> orders)
        {
            FileTracing.TraceOut("GetDerivativesOrderBook", TraceType.Noise);
            orders = null;
            // Login If needed
            BrokerErrorCode errorCode = CheckAndLogInIfNeeded(false);
            if (errorCode != BrokerErrorCode.Success)
                return errorCode;

            //if (!(instrumentType == InstrumentType.FutureIndex || instrumentType == InstrumentType.FutureStock ||
            //    instrumentType == InstrumentType.OptionCallIndex || instrumentType == InstrumentType.OptionCallStock ||
            //    instrumentType == InstrumentType.OptionPutIndex || instrumentType == InstrumentType.OptionPutStock)
            //    && errorCode == BrokerErrorCode.Success)
            //{
            //    errorCode = BrokerErrorCode.InValidArg;
            //}

            string postData = "FFO_PASS=1&FFO_ACCOUNT=" +
                ACCOUNTNUM +
                "&FFO_FRM_DT=" +
                fromDate.ToString("dd") + "%2F" + fromDate.ToString("MM") + "%2F" + fromDate.ToString("yyyy") +
                "&FFO_TO_DT=" +
                toDate.ToString("dd") + "%2F" + toDate.ToString("MM") + "%2F" + toDate.ToString("yyyy") +
                "&FFO_XCHNG_CD=NFO" +
                "&FFO_PRDCT_TYP=" + //((instrumentType == InstrumentType.FutureIndex || instrumentType == InstrumentType.FutureStock) ? "F" : "O") +
                "&FML_ORDR_ST=A" +
                "&FML_ORDR_ACTION=A&Submit=+View+";

            string orderBookData = IciciGetWebPageResponse(URL_ICICI_FNO_ORDERBOOK,
                postData,
                URL_ICICI_FNO_HOME,
                mCookieContainer,
                out errorCode);

            orders = new Dictionary<string, DerivativeOrderBookRecord>();

            if (errorCode.Equals(BrokerErrorCode.Success) && !orderBookData.Contains("No matching Orders"))
            {
                string subOrderBookData = StringParser.GetStringBetween(orderBookData,
                    0,
                    "<table width=\"100%\" class=\"projection\"",
                    "</table>",
                    new string[] { });
                ParsedTable table = (ParsedTable)HtmlTableParser.ParseHtmlIntoTables("<table>" + subOrderBookData + "</table>", true);
                //table.PrintTable(null, Console.Out);
                for (int i = 1; i < table.RowCount; i++)
                {
                    DerivativeOrderBookRecord info = new DerivativeOrderBookRecord();
                    info.ContractName = table[i, 0].ToString().Trim();
                    var tempArr = info.ContractName.Split(new string[] { "-" }, StringSplitOptions.None);
                    info.UnderlyingSymbol = tempArr[1];
                    //info.InstrumentType = tempArr[0] == "FUT" ? InstrumentType.FutureIndex : tempArr[tempArr.Length - 1] == "CE" ?
                    string tempStr = table[i, 3].ToString().ToUpperInvariant();
                    info.Direction = (OrderDirection)Enum.Parse(typeof(OrderDirection), tempStr);
                    info.OrderDate = DateTime.Parse(table[i, 1].ToString());
                    info.Quantity = int.Parse(table[i, 4].ToString());
                    tempStr = table[i, 5].ToString();
                    tempStr = StringParser.GetStringBetween(tempStr, 0, ">", "</a>", null);
                    tempStr = StringParser.GetStringBetween(tempStr, 0, ">", "</font>", null);
                    info.Price = double.Parse(tempStr);
                    string orderRefString = table[i, 2].ToString();
                    orderRefString = StringParser.GetStringBetween(orderRefString, 0, ">", "</a>", null);
                    info.OrderRefenceNumber = orderRefString.Trim();
                    tempStr = table[i, 6].ToString().Trim().ToUpper();
                    // Executed orders have different string format, so ignore the clutter
                    if (tempStr.Contains("EXECUTED"))
                    {
                        tempStr = "EXECUTED";
                    }
                    info.OrderStatus = (OrderStatus)Enum.Parse(typeof(OrderStatus), tempStr);

                    info.OpenQty = int.Parse(table[i, 8].ToString());
                    info.ExecutedQty = int.Parse(table[i, 9].ToString());
                    info.CancelledQty = int.Parse(table[i, 10].ToString());
                    info.ExpiredQty = int.Parse(table[i, 11].ToString());
                    tempStr = StringParser.GetCleanedupString(table[i, 12].ToString());
                    if (!string.IsNullOrEmpty(tempStr))
                        info.StopLossPrice = double.Parse(tempStr);

                    // Only add valid outstanding orders if bOnlyOutstandingOrders is true
                    // PARTEXEC is considered OUTSTANDING (i.e. not considered EXEC until fully executed)
                    if (!bOnlyOutstandingOrders ||
                        info.OrderStatus == OrderStatus.PARTEXEC ||
                        info.OrderStatus == OrderStatus.QUEUED ||
                        info.OrderStatus == OrderStatus.REQUESTED ||
                        info.OrderStatus == OrderStatus.ORDERED)
                    {
                        lock (lockObjectDerivatives)
                        {
                            if (mDerivativeOrderBook.ContainsKey(info.OrderRefenceNumber))
                            {
                                if (!newTradesOnly)
                                    orders.Add(info.OrderRefenceNumber, info);
                                // Update the order
                                mDerivativeOrderBook[info.OrderRefenceNumber] = info;
                            }
                            else
                            {
                                mDerivativeOrderBook.Add(info.OrderRefenceNumber, info);
                                orders.Add(info.OrderRefenceNumber, info);
                            }
                        }
                    }
                }
                // Insert to Database
                if (errorCode == BrokerErrorCode.Success)
                {
                    //MUNISHTODOREMOVE:StockUtils.PersistOrderbookRecords(orders);
                }

                FileTracing.TraceOut(!errorCode.Equals(BrokerErrorCode.Success), "GetDerivativesOrderBook:" + errorCode.ToString(), TraceType.Error);
            }
            return errorCode;
        }

        public BrokerErrorCode GetDerivativesTradeBook(DateTime fromDate,
            DateTime toDate,
            InstrumentType instrumentType,
            bool newTradesOnly,
            out Dictionary<string, DerivativeTradeBookRecord> trades)
        {
            trades = null;
            FileTracing.TraceOut("GetDerivativesTradeBook", TraceType.Noise);
            // Login If needed
            BrokerErrorCode errorCode = CheckAndLogInIfNeeded(false);
            if (errorCode != BrokerErrorCode.Success)
                return errorCode;

            string postData = "FFO_PASS=1&FFO_ACCOUNT=" +
                ACCOUNTNUM +
                "&FFO_FRM_DT=" +
                fromDate.ToString("dd") + "%2F" + fromDate.ToString("MM") + "%2F" + fromDate.ToString("yyyy") +
                "&FFO_TO_DT=" +
                toDate.ToString("dd") + "%2F" + toDate.ToString("MM") + "%2F" + toDate.ToString("yyyy") +
                "&FFO_XCHNG_CD=NFO" +
                "&FFO_PRDCT_TYP=" + ((instrumentType == InstrumentType.FutureIndex || instrumentType == InstrumentType.FutureStock) ? "F" : "O") +
                "&FML_ORDR_ACTION=A&Submit=+View+";

            string tradeBookData = IciciGetWebPageResponse(URL_ICICI_FNO_TRADEBOOK,
                postData,
                null,
                mCookieContainer,
                out errorCode);

            if (errorCode.Equals(BrokerErrorCode.Success) && !tradeBookData.Contains("No matching Orders"))
            {
                trades = new Dictionary<string, DerivativeTradeBookRecord>();
                string subTradeBookData = StringParser.GetStringBetween(tradeBookData,
                    0,
                    "<table width=\"100%\" class=\"projection\">",
                    "</table>",
                    new string[] { });

                ParsedTable table = (ParsedTable)HtmlTableParser.ParseHtmlIntoTables("<table>" + subTradeBookData + "</table>", true);

                for (int i = 1; i < table.RowCount - 1; i++)
                {
                    DerivativeTradeBookRecord info = new DerivativeTradeBookRecord();
                    info.ContractName = table[i, 1].ToString().Trim();
                    if (info.ContractName.EndsWith("-I"))
                        info.ContractName = info.ContractName.Substring(0, info.ContractName.Length - 2);
                    var tempArr = info.ContractName.Split(new string[] { "-" }, StringSplitOptions.None);
                    info.UnderlyingSymbol = tempArr[1];
                    //info.InstrumentType = tempArr[0] == "FUT" ? InstrumentType.FutureIndex : tempArr[tempArr.Length - 1] == "CE" ?
                    info.TradeDate = DateTime.Parse(table[i, 0].ToString());
                    string tempStr = table[i, 2].ToString().ToUpperInvariant();
                    info.Direction = (OrderDirection)Enum.Parse(typeof(OrderDirection), tempStr);
                    info.Quantity = int.Parse(table[i, 3].ToString());
                    info.Price = double.Parse(table[i, 4].ToString());
                    info.TradeValue = double.Parse(table[i, 5].ToString());
                    tempStr = table[i, 6].ToString();
                    if (!double.TryParse(tempStr, out info.Brokerage))
                    {
                        tempStr = StringParser.GetStringBetween(tempStr, 0, ">", "</a>", null).Trim();
                        info.Brokerage = double.Parse(tempStr);
                    }
                    string orderRefString = table[i, 7].ToString();
                    info.OrderRefenceNumber = StringParser.GetStringBetween(orderRefString, 0, ">", "</a>", null).Trim();

                    lock (lockObjectDerivatives)
                    {
                        if (mDerivativeTradeBook.ContainsKey(info.OrderRefenceNumber))
                        {
                            if (!newTradesOnly)
                                trades.Add(info.OrderRefenceNumber, info);
                        }
                        else
                        {
                            mDerivativeTradeBook.Add(info.OrderRefenceNumber, info);
                            trades.Add(info.OrderRefenceNumber, info);
                        }
                    }
                }
            }

            // Insert to Database
            if (errorCode == BrokerErrorCode.Success)
            {
                //MUNISHTODOREMOVE:StockUtils.PersistTradebookRecords(trades);
            }

            FileTracing.TraceOut(!errorCode.Equals(BrokerErrorCode.Success), "GetDerivativesTradeBook:" + errorCode.ToString(), TraceType.Error);
            return errorCode;
        }

        private BrokerErrorCode RefreshDerivativesOrderBookToday()
        {
            DateTime EarliestValidMarketOpenDate = MarketUtils.GetMarketCurrentDate();
            Dictionary<string, DerivativeOrderBookRecord> orders;
            return GetDerivativesOrderBook(EarliestValidMarketOpenDate, EarliestValidMarketOpenDate, InstrumentType.FutureStock, false, false, out orders);
        }

        public BrokerErrorCode CancelDerivativesOrder(string orderRef, bool refreshOrderBook)
        {
            // Login If needed
            BrokerErrorCode errorCode = CheckAndLogInIfNeeded(false);
            if (errorCode != BrokerErrorCode.Success)
                return errorCode;

            //bool isFuture = false;
            //bool isIndex = false;
            //bool isCall = instrumentType == InstrumentType.OptionCallIndex || instrumentType == InstrumentType.OptionCallStock;

            //if (instrumentType == InstrumentType.FutureIndex || instrumentType == InstrumentType.FutureStock)
            //{
            //    isFuture = true;
            //    contract = "FUT-" + contract;
            //}
            //else if (instrumentType != InstrumentType.Share)
            //    contract = string.Format("{0}-{1}-{2}-{3}", "OPT", contract, (int)strikePrice, isCall ? "CE" : "PE");

            if (refreshOrderBook)
            {
                // Make sure we have the latest order book data
                errorCode = RefreshDerivativesOrderBookToday();
                if (errorCode != BrokerErrorCode.Success)
                    return errorCode;
            }

            DerivativeOrderBookRecord orderInfo;
            // Search for order in the Orders dictionary
            lock (lockObjectDerivatives)
            {
                if (mDerivativeOrderBook.ContainsKey(orderRef))
                {
                    orderInfo = mDerivativeOrderBook[orderRef];
                }
                else
                {
                    return BrokerErrorCode.OrderDoesNotExist;
                }
            }

            if (orderInfo.OrderStatus != OrderStatus.ORDERED &&
                orderInfo.OrderStatus != OrderStatus.PARTEXEC &&
                orderInfo.OrderStatus != OrderStatus.REQUESTED)
            {
                if (orderInfo.OrderStatus == OrderStatus.QUEUED)
                    errorCode = BrokerErrorCode.OrderQueuedCannotCancel;
                else if (orderInfo.OrderStatus == OrderStatus.EXECUTED)
                    errorCode = BrokerErrorCode.OrderExecutedCannotCancel;
                else if (orderInfo.OrderStatus == OrderStatus.CANCELLED)
                    errorCode = BrokerErrorCode.OrderAlreadyCancelled;
                else
                    errorCode = BrokerErrorCode.InValidOrderToCancel;
                return errorCode;
            }

            // Instead of getting it actually from order book
            // We simply assume current day only
            DateTime currentDate = MarketUtils.GetMarketCurrentDate();
            string orderStatus = IciciUtils.OrderStatusString[(int)orderInfo.OrderStatus];
            string zipCode = orderRef.Substring(8, 2);

            string postData = "FML_PASS=1" +
                "&FFO_DT=" + // TODO: Use month name calculation , currently hardcoded "Nov"
                currentDate.ToString("dd") + "-" + currentDate.ToString("MMM") + "-" + currentDate.ToString("yyyy") +
                //"&FML_STATUS=" + orderStatus +
                "&FML_ORDR_RFRNC=" + orderRef +
                // TODO: C for CASH , currently hardcoded. needs to be handled. 
                // Get it in order book stock info somehow
                "&FML_ORD_PRDCT_TYP=O" +
                "&FML_ORD_ORDR_FLW=" + IciciUtils.OrderDirectionString[(int)orderInfo.Direction] + // order direction S/B
                "&FML_ACCOUNT=" + ACCOUNTNUM +
                "&FML_ORD_XCHNG_CD=NFO" + //IciciUtils.ExchangeString[(int)orderInfo.Exchange] + // get exchange
                "&FFO_ORD_TOT_QTY=" + orderInfo.Quantity +
                "&FFO_UNDRLYNG=" + orderInfo.UnderlyingSymbol +
                "&Button1=Yes&Button2=No";

            //"&FML_TO_DT=" +
            //toDate.ToString("dd") + "%2F" + toDate.ToString("MM") + "%2F" + toDate.ToString("yyyy") +
            //"&STCK_CD=All" +
            //"&FML_XCHNG_CD=A" +
            //"&PRDCT_TYPE=A" +
            //"&FML_ORDR_ST=A" +
            //"&FML_ORDR_ACTION=A&Submit=+View+" +
            //"&FML_FOCUS_FLAG=D" +
            //"&FML_TMP_FRM_DT=" + tmpDateToday +
            //"&FML_TMP_TO_DT=" + tmpDateToday +
            //"&FML_ORD_PRDCT_TYP=A" +
            //"&FML_strfinal=" + tmpDateLater +
            //"&FML_ORDR_RFRNC=" + "&FML_ORDR_FLW=" +
            //"&FML_TMP_STTLMNT_NMBR=" +
            //"&FML_STCK_CD=" +
            ////"&FML_SESSION_ID=" + FML_SESSION_ID +
            //"&FML_ORD_DP_ID=" + "&FML_CLNT_ID=" + "&Update=" + "&ButtonDis=" + "&x=" + "&HideLayer=";

            string orderBookData = IciciGetWebPageResponse(URL_ICICI_FNO_CANCEL_ORDER,
                postData,
                URL_ICICI_FNO_HOME,
                mCookieContainer,
                out errorCode);
            //Expiry is under way,
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
                    // because let the GetEquityOrderBook update it as per its protocol
                    lock (lockObjectDerivatives)
                    {
                        mDerivativeOrderBook.Remove(orderRef);
                    }
                    return BrokerErrorCode.Success;
                }
            }

            return BrokerErrorCode.OrderCancelFailed;
        }
    }
}