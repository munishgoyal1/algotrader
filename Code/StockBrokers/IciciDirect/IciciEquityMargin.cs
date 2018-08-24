using System;
using StockTrader.Core;


namespace StockTrader.Broker.IciciDirect
{
    public partial class IciciDirectBroker
    {
        // place margin order
        public BrokerErrorCode PlaceEquityMarginPLUSOrder(string stockCode,
            int quantity,
            string stopLossPrice,
            string limitPrice,
            OrderDirection orderDirection)
        {
            // Login If needed
            BrokerErrorCode errorCode = CheckAndLogInIfNeeded(false);
            if (errorCode != BrokerErrorCode.Success)
            {
                return errorCode;
            }

            string FML_ORD_XCHNG_CD_value = "NSE";
            string FML_ORD_XCHNG_CD_C_value = FML_ORD_XCHNG_CD_value;
            // order direction
            string FML_ORD_ORDR_FLW_value = null;
            string FML_ORD_ORDR_FLW_C_value = null;
            string oppositeDirection = "";
            string direction = "";
            if (orderDirection == OrderDirection.BUY)
            {
                FML_ORD_ORDR_FLW_value = "B";
                FML_ORD_ORDR_FLW_C_value = "S";
                direction = "BUY";
                oppositeDirection = "SELL";
            }
            else if (orderDirection == OrderDirection.SELL)
            {
                FML_ORD_ORDR_FLW_value = "S";
                FML_ORD_ORDR_FLW_C_value = "B";
                direction = "SELL";
                 oppositeDirection = "BUY";
            }
            // prices
            string FML_ORD_LMT_RT_value = limitPrice;
            string FML_ORD_STP_LSS_value = stopLossPrice;
            //string FML_ORD_TRD_DT_value = FML_ORD_TRD_DT_NSE_value;

            string query =
            "FML_XCHNG_ST0=O&FML_XCHNG_ST1=O" +
            "&FML_ACCOUNT=" + FML_ACCOUNT_value +
            "&FML_ORD_XCHNG_CD=" + FML_ORD_XCHNG_CD_value +
            "&FML_STCK_CD=" + stockCode +
            "&FML_ORD_ORDR_FLW=" + FML_ORD_ORDR_FLW_value +
            "&FML_DOTNET_FLG=Y&FML_URL_FLG=http%3A%2F%2Fgetquote.icicidirect.com%2FNewSiteTrading%2Ftrading%2Fequity%2Fincludes%2Ftrading_stock_quote.asp" +
            "&FML_QTY=" + quantity.ToString() +
            "&FML_ORD_TYP=M" + 
            "&FML_ORD_XCHNG_CD_C=" + FML_ORD_XCHNG_CD_value +
            "&FML_STCK_CD_C=" + stockCode +
            "&FML_STCK_CD_HIDDEN=" + stockCode +
            "&FML_ORD_ORDR_FLW_C=" + oppositeDirection + 
            "&FML_ORD_ORDR_FLW_HIDDEN=" + oppositeDirection +
            "&FML_ORD_TYP_C=L" +
             "&FML_ORD_LMT_RT=" + FML_ORD_LMT_RT_value +
                "&FML_ORD_DSCLSD_QTY=" +
                "&FML_ORD_STP_LSS=" + FML_ORD_STP_LSS_value +
                "&FML_QTY_C=" + quantity.ToString() + 
                "&FML_QTY_HIDDEN=" + quantity.ToString() +
"&FML_ORD_XCHNG_CD_P=" +
"&FML_STCK_CD_P=" + stockCode +
"&FML_ORD_ORDR_FLW_P=" + oppositeDirection +
"&FML_ORD_TYP_P=L" +
"&FML_ORD_LMT_RT_P=" +
"&FML_QTY_P=" +
"&Submit=Submit" +
"&FML_ORD_TRD_DT_BSE=04-Apr-2012" +
"&FML_ORD_TRD_DT_NSE=04-Apr-2012" +
"&FML_ORD_TRD_DT=" +
"&FML_SESSION_ID=85730071" + // GET Session ID
"&FML_PRODUCT_INDEX=" +
"&FML_ORD_PRD_HIDDEN=" +
"&FML_ORD_ACCOUNT_HIDDEN=" +
"&FML_ACCOUNT_INDEX=" +
"&FML_XCHNG_ST=O" +
"&FML_ORD_CLM_MTCH_ACCNT=" + ACCOUNTNUM +
                "&FML_ORD_DP_CLNT_ID=" + CLIENTID +
                "&FML_ORD_DP_ID=" + DPID +
"&FML_TRN_PRDT_TYP=" +
"&FML_ORD_XCHNG_CD_CHECK=" +
"&FML_PRCNTG_CHECK=" +
"&NicValue=" +
"&BrowserBack_Xchang=" +
"&FML_LAS=Y" +
"&m_FML_AC_ACTIVATED_FROM=BSE" +
"&m_FML_USR_ZIP_CD=B8" +
"&m_FML_AC_ACTIVATED_FROM=NSE" +
"&m_FML_USR_ZIP_CD=N2";


            string orderPlacePageData = IciciGetWebPageResponse(URL_ICICI_EQT_MARGINPLUS_ORDER,
                query,
                URL_ICICI_REFERRER,
                mCookieContainer,
                out errorCode);

            string time = DateTime.Now.ToString("dd-MMM-yyyy") + " " + DateTime.Now.ToString("hh:mm:ss");

            query =
                "m_TuxServerName:testjolt" +
                "&FML_QTY=" + quantity.ToString() +
                "&FML_STCK_CD=" + stockCode +
                "&FML_ORD_PRDCT_TYP=" +
                "&FML_ORD_TYP=M" +
                "&FML_REFNUM=" +
                "&FML_XCHNG_ST=O" +
                "&FML_ACCOUNT=" + FML_ACCOUNT_value +
                "&FML_ORD_XCHNG_CD=" + FML_ORD_XCHNG_CD_value +
                "&FML_ORD_LMT_RT=" + FML_ORD_LMT_RT_value +
                "&FML_ORD_DSCLSD_QTY=" +
                "&FML_ORD_STP_LSS=" + FML_ORD_STP_LSS_value +
                "&FML_ORD_TRD_DT=" + //FML_ORD_TRD_DT_NSE_value +
                "&FML_ORD_ORDR_FLW=" + FML_ORD_ORDR_FLW_value +
                //"&FML_ORD_EXCTD_RT=" + FML_ORD_LMT_RT_value +
                "&FML_PASS=1" +
                "&FML_ORD_CLM_MTCH_ACCNT=" + ACCOUNTNUM +
                "&FML_ORD_DP_CLNT_ID=" + CLIENTID +
                "&FML_ORD_DP_ID=" + DPID +
                "&FML_XCHNG_SG=N" +
                //"&FML_ORD_XCHNG_SGMNT_STTLMNT=2012065" +
                 //"&FML_DEMAT_IN_DT=08-Apr-2012&FML_DEMAT_OUT_DT=11-Apr-2012&FML_FUND_IN_DT=09-Apr-2012&FML_FUND_OUT_DT=10-Apr-2012" +
                 "&FML_ISIN_NUMBER=" +
                 "&FML_TM=" + time + 
                 "&FML_QUOTE_TIME=" + time +
                "&FML_CVR_QTY=0.0" +
                "&FML_STK_STCK_NM=XYZ" +
                "&FML_RQST_TYP=&FML_TRN_PRDT_TYP=&FML_SCHM_ID=&FML_RT=&FML_ORD_PRD_HIDDEN=" +
                "&FML_ORD_ORDR_FLW_HIDDEN=" + oppositeDirection +
                "&FML_GMS_CSH_PRDCT_PRCNTG=0&FML_PRODUCT_INDEX=&FML_ACCOUNT_INDEX=&FML_MULTI_ROW_CHECK=" + 
                "&FML_TRADING_PASS_FLAG=N" +
                "&ATValue=&FML_TRADING_ALIAS=&x=&FML_ORD_LMT_RT_P=" +
                "&ProChk=&Submit=Proceed" +
                "&button=Back&button=Back"; 
         
            if (errorCode.Equals(BrokerErrorCode.Success))
            {
                orderPlacePageData = IciciGetWebPageResponse(URL_ICICI_EQT_MARGINPLUS_ORDER,
                    query,
                    URL_ICICI_REFERRER,
                    mCookieContainer,
                    out errorCode);

                if (errorCode.Equals(BrokerErrorCode.Success))
                {
                    errorCode = GetEQTOrderPlacementCode(orderPlacePageData, EquityOrderType.MARGIN);
                    if (BrokerErrorCode.Success == errorCode)
                    {
                        // Get exchange reference number (primary key for an order)
                        // add the record to DB
                        // TODO: is it required since we already have parser of order & trade book
                        GetOrderConfirmationData(orderPlacePageData, EquityOrderType.MARGIN);

                    }
                }
            }

            return errorCode;
        }

        // TOTEST
        // place margin order
        public BrokerErrorCode PlaceEquityMarginOrder(string stockCode,
            int quantity,
            string stopLossPrice,
            string limitPrice,
            OrderDirection orderDirection)
        {
            // Login If needed
            BrokerErrorCode errorCode = CheckAndLogInIfNeeded(false);
            if (errorCode != BrokerErrorCode.Success)
            {
                return errorCode;
            }

            string FML_ORD_XCHNG_CD_value = "NSE";
            string FML_ORD_XCHNG_CD_C_value = FML_ORD_XCHNG_CD_value;
            // order direction
            string FML_ORD_ORDR_FLW_value = null;
            string FML_ORD_ORDR_FLW_C_value = null;
            if (orderDirection == OrderDirection.BUY)
            {
                FML_ORD_ORDR_FLW_value = "B";
                FML_ORD_ORDR_FLW_C_value = "S";
            }
            else if (orderDirection == OrderDirection.SELL)
            {
                FML_ORD_ORDR_FLW_value = "S";
                FML_ORD_ORDR_FLW_C_value = "B";
            }
            // prices
            string FML_ORD_LMT_RT_value = limitPrice;
            string FML_ORD_STP_LSS_value = stopLossPrice;
            //string FML_ORD_TRD_DT_value = FML_ORD_TRD_DT_NSE_value;

            string query = ("FML_ORD_ORDR_FLW=" + FML_ORD_ORDR_FLW_value +
                "FML_ORD_ORDR_FLW_C=" + FML_ORD_ORDR_FLW_C_value +
                "&FML_ACCOUNT=" + FML_ACCOUNT_value +
                "&FML_STCK_CD=" + stockCode +
                "&FML_STCK_CD_C=" + stockCode +
                "&FML_DOTNET_FLG=Y&FML_URL_FLG=http%3A%2F%2Fgetquote.icicidirect.com%2Ftrading%2Fequity%2Ftrading_stock_quote.asp " +
                "&FML_QTY=" + quantity.ToString() +
                "&FML_QTY_C=" + quantity.ToString() +
                "&FML_ORD_TYP=M" +
                "&FML_ORD_TYP_C=L" +
                "&FML_ORD_STP_LSS=" + FML_ORD_STP_LSS_value +
                "&FML_ORD_LMT_RT=" + FML_ORD_LMT_RT_value +

                "&FML_ORD_TRD_DT_BSE=" + FML_ORD_TRD_DT_BSE_value +
                "&FML_ORD_TRD_DT_NSE=" + FML_ORD_TRD_DT_NSE_value +
                "&FML_ORD_TRD_DT=" + FML_ORD_TRD_DT_NSE_value +
                "&FML_PRODUCT_INDEX=0&FML_ORD_PRD_HIDDEN=CASH&FML_ORD_CLM_MTCH_ACCNT=" +
                "&FML_TRADING_LIMIT_NSE=" + FML_TRADING_LIMIT_NSE_value +
                "&FML_TRADING_LIMIT_BSE=" + FML_TRADING_LIMIT_BSE_value +
                "&FML_ORD_DP_CLNT_ID=&FML_ORD_DP_ID=&FML_TRN_PRDT_TYP=" +
                "&FML_ORD_XCHNG_CD=" + FML_ORD_XCHNG_CD_value +
                "&FML_ORD_XCHNG_CD_C=" + FML_ORD_XCHNG_CD_C_value +
               "&FML_ORD_XCHNG_CD_CHECK=&FML_PRCNTG_CHECK=3.0&FML_ARRAY_BOUND=1&FML_ARRAY_ELEMENT=&NicValue=&BrowserBack_Xchang=NSE&PWD_ENABLED=N&FML_LAS=Y");

            string orderPlacePageData = IciciGetWebPageResponse(
                URL_ICICI_EQT_CASHMARGIN_ORDER,
                query,
               URL_ICICI_REFERRER,
                mCookieContainer,
                out errorCode);

            if (errorCode.Equals(BrokerErrorCode.Success))
            {
                errorCode = GetEQTOrderPlacementCode(orderPlacePageData, EquityOrderType.MARGIN);
                if (BrokerErrorCode.Success == errorCode)
                {
                    // Get exchange reference number (primary key for an order)
                    // add the record to DB
                    // TODO: is it required since we already have parser of order & trade book
                    GetOrderConfirmationData(orderPlacePageData, EquityOrderType.MARGIN);

                }
            }

            return errorCode;
        }

    }


}