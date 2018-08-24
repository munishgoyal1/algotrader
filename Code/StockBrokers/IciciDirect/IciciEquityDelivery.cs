using System;
using StockTrader.Core;


namespace StockTrader.Broker.IciciDirect
{
    public partial class IciciDirectBroker
    {

        // place deliver order
        public BrokerErrorCode PlaceEquityMarginDeliveryFBSOrder(string stockCode,
            int quantity,
            string price,
            OrderPriceType orderPriceType,
            OrderDirection orderDirection,
            EquityOrderType orderType,
            Exchange exchange)
        {

            // Login If needed
            BrokerErrorCode errorCode = CheckAndLogInIfNeeded(false);
            if (errorCode != BrokerErrorCode.Success)
                return errorCode;

            string FML_ORD_ORDR_FLW_value = null;
            if (orderDirection == OrderDirection.BUY) FML_ORD_ORDR_FLW_value = "B";
            else if (orderDirection == OrderDirection.SELL) FML_ORD_ORDR_FLW_value = "S";
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
            string squareOffMode = "S";
            string FML_ORD_XCHNG_CD_value = null;
            if (exchange == Exchange.BSE)
            {
                FML_ORD_TRD_DT_value = FML_ORD_TRD_DT_BSE_value;
                FML_ORD_XCHNG_CD_value = "BSE";
                squareOffMode = "S";
            }
            else if (exchange == Exchange.NSE)
            {
                FML_ORD_TRD_DT_value = FML_ORD_TRD_DT_NSE_value;
                FML_ORD_XCHNG_CD_value = "NSE";
                squareOffMode = "M";
            }

            string prdctType = orderType == EquityOrderType.DELIVERY ? "CASH" : "MARGIN";

            string query = "FML_ORD_ORDR_FLW=" + FML_ORD_ORDR_FLW_value +
                "&FML_ACCOUNT=" + FML_ACCOUNT_value +
                "&TEMP=" + FML_ORD_XCHNG_CD_value +
                "&FML_ORD_PRDCT_TYP=" + prdctType +
                (orderType == EquityOrderType.MARGIN ? "&FML_SQ_FLAG=" + squareOffMode : "") +  //Squareoff mode: M (for client) S (for broker), for BSE only S
                "&FML_STCK_CD=" + stockCode +
                "&FML_QTY=" + quantity.ToString() +
                "&FML_DOTNET_FLG=Y&FML_URL_FLG=http%3A%2F%2Fgetquote.icicidirect.com%2Ftrading%2Fequity%2Ftrading_stock_quote.asp " +
                "&FML_ORD_TYP=" + FML_ORD_TYP_value +
                "&FML_ORD_LMT_RT=" + FML_ORD_LMT_RT_value +
                "&FML_ORD_DSCLSD_QTY=&FML_ORD_STP_LSS=" +
                "&FML_ORD_TRD_DT_BSE=" + FML_ORD_TRD_DT_BSE_value +
                "&FML_ORD_TRD_DT_NSE=" + FML_ORD_TRD_DT_NSE_value +
                "&FML_ORD_TRD_DT=" + FML_ORD_TRD_DT_value +
                "&FML_PRODUCT_INDEX=0" +
                "&FML_ORD_PRD_HIDDEN=" + prdctType +
                "&FML_ORD_CLM_MTCH_ACCNT=" +
                "&FML_TRADING_LIMIT_NSE=" + FML_TRADING_LIMIT_NSE_value +
                "&FML_TRADING_LIMIT_BSE=" + FML_TRADING_LIMIT_BSE_value +
                "&FML_ORD_DP_CLNT_ID=&FML_ORD_DP_ID=&FML_TRN_PRDT_TYP=" +
                "&FML_ORD_XCHNG_CD=" + FML_ORD_XCHNG_CD_value +
                "&FML_ORD_XCHNG_CD_CHECK=" + (exchange == Exchange.BSE ? "NSE" : "") +
                "&FML_PRCNTG_CHECK=3.0&FML_ARRAY_BOUND=1&FML_ARRAY_ELEMENT=&NicValue=&BrowserBack_Xchang=NSE&PWD_ENABLED=N&FML_LAS=Y" +
                "&m_FML_AC_ACTIVATED_FROM=BSE&m_FML_USR_ZIP_CD=B3&m_FML_AC_ACTIVATED_FROM=NSE&m_FML_USR_ZIP_CD=N9";

            string orderPlacePageData = IciciGetWebPageResponse(URL_ICICI_EQT_FBS_CASHMARGIN_ORDER,
                query,
                URL_ICICI_REFERRER,
                mCookieContainer,
                out errorCode);

            if (errorCode.Equals(BrokerErrorCode.Success))
            {

                errorCode = GetEQTOrderPlacementCode(orderPlacePageData, EquityOrderType.DELIVERY);
                if (BrokerErrorCode.Success == errorCode)
                {
                    // ********** Get exchange reference number (primary key for an order)  ***********
                    // add the record to DB
                    GetOrderConfirmationData(orderPlacePageData, orderType);
                }
            }
            return errorCode;
        }

        // place deliver order
        public BrokerErrorCode PlaceEquityDeliveryOrder(string stockCode,
            int quantity,
            string price,
            OrderPriceType orderPriceType,
            OrderDirection orderDirection,
            Exchange exchange)
        {

            // Login If needed
            BrokerErrorCode errorCode = CheckAndLogInIfNeeded(false);
            if (errorCode != BrokerErrorCode.Success)
            {
                return errorCode;
            }

            string FML_ORD_ORDR_FLW_value = null;
            if (orderDirection == OrderDirection.BUY) FML_ORD_ORDR_FLW_value = "B";
            else if (orderDirection == OrderDirection.SELL) FML_ORD_ORDR_FLW_value = "S";
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

            string query = ("FML_ORD_ORDR_FLW=" + FML_ORD_ORDR_FLW_value +
                "&FML_ACCOUNT=" + FML_ACCOUNT_value +
                "&TEMP=NSE&FML_ORD_PRDCT_TYP=CASH&FML_STCK_CD=" + stockCode +
                "&FML_QTY=" + quantity.ToString() +
                "&FML_DOTNET_FLG=Y&FML_URL_FLG=http%3A%2F%2Fgetquote.icicidirect.com%2Ftrading%2Fequity%2Ftrading_stock_quote.asp " +
                "&FML_ORD_TYP=" + FML_ORD_TYP_value +
                "&FML_ORD_LMT_RT=" + FML_ORD_LMT_RT_value +
                "&FML_ORD_DSCLSD_QTY=&FML_ORD_STP_LSS=" +
                "&FML_ORD_TRD_DT_BSE=" + FML_ORD_TRD_DT_BSE_value +
                "&FML_ORD_TRD_DT_NSE=" + FML_ORD_TRD_DT_NSE_value +
                "&FML_ORD_TRD_DT=" + FML_ORD_TRD_DT_NSE_value +
                "&FML_PRODUCT_INDEX=0&FML_ORD_PRD_HIDDEN=CASH&FML_ORD_CLM_MTCH_ACCNT=" +
                "&FML_TRADING_LIMIT_NSE=" + FML_TRADING_LIMIT_NSE_value +
                "&FML_TRADING_LIMIT_BSE=" + FML_TRADING_LIMIT_BSE_value +
                "&FML_ORD_DP_CLNT_ID=&FML_ORD_DP_ID=&FML_TRN_PRDT_TYP=" +
                "&FML_ORD_XCHNG_CD=" + FML_ORD_XCHNG_CD_value +
                "&FML_ORD_XCHNG_CD_CHECK=&FML_PRCNTG_CHECK=3.0&FML_ARRAY_BOUND=1&FML_ARRAY_ELEMENT=&NicValue=&BrowserBack_Xchang=NSE&PWD_ENABLED=N&FML_LAS=Y" +
                "&m_FML_AC_ACTIVATED_FROM=BSE&m_FML_USR_ZIP_CD=B3&m_FML_AC_ACTIVATED_FROM=NSE&m_FML_USR_ZIP_CD=N9");

            string orderPlacePageData = IciciGetWebPageResponse(URL_ICICI_EQT_CASHMARGIN_ORDER,
                query,
                URL_ICICI_REFERRER,
                mCookieContainer,
                out errorCode);

            if (errorCode.Equals(BrokerErrorCode.Success))
            {

                errorCode = GetEQTOrderPlacementCode(orderPlacePageData, EquityOrderType.DELIVERY);
                if (BrokerErrorCode.Success == errorCode)
                {
                    // Get exchange reference number (primary key for an order)
                    // add the record to DB
                    GetOrderConfirmationData(orderPlacePageData, EquityOrderType.DELIVERY);
                }
            }
            return errorCode;
        }
    }
}