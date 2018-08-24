using System;
using System.Collections.Generic;
using StockTrader.Core;
using StockTrader.Platform.Logging;

namespace StockTrader.Broker.IciciDirect
{
    public partial class IciciDirectBroker
    {
        // Get equity order placed return code
        public BrokerErrorCode GetEQTOrderPlacementCode(string orderResponse, EquityOrderType eqOrderType)
        {
            BrokerErrorCode code = BrokerErrorCode.Unknown;

            //Margin trading temporarily suspended

            if (null == orderResponse)
            {
                return BrokerErrorCode.NullResponse;
            }
            if (orderResponse.IndexOf("rder placed successfully", StringComparison.OrdinalIgnoreCase) > 0)
            {
                return BrokerErrorCode.Success;
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

        public BrokerErrorCode GetOrderStatus(string orderNumber,
            InstrumentType instrumentType,
            DateTime fromDate,
            DateTime toDate,
            out OrderStatus orderStatus)
        {
            BrokerErrorCode errorCode = BrokerErrorCode.Success;
            orderStatus = OrderStatus.NOTFOUND;

            if (instrumentType == InstrumentType.Share)
            {
                Dictionary<string, EquityOrderBookRecord> orders;

                errorCode = GetEquityOrderBook(fromDate,
                             toDate,
                             false,
                             false,
                             null,
                             out orders);

                if (errorCode.Equals(BrokerErrorCode.Success))
                {
                    foreach (KeyValuePair<string, EquityOrderBookRecord> orderPair in orders)
                    {
                        EquityOrderBookRecord order = orderPair.Value;
                        if (order.OrderRefenceNumber == orderNumber)
                        {
                            orderStatus = order.Status;
                            break;
                        }
                    }
                }
            }

            FileTracing.TraceOut(!errorCode.Equals(BrokerErrorCode.Success), "GetOrderStatus:" + errorCode.ToString(), TraceType.Error);
            return errorCode;
        }

        public double GetTradeExecutionPrice(DateTime fromDate,
            DateTime toDate,
            InstrumentType instrumentType,
            string orderRefString)
        {
            double executionPrice = -1;

            BrokerErrorCode errorCode = BrokerErrorCode.Success;

            if (instrumentType.Equals(InstrumentType.Share))
            {
                errorCode = RefreshEquityTradeBook(this, fromDate, toDate, false);
                if (!errorCode.Equals(BrokerErrorCode.Success))
                    return executionPrice;
                lock (lockObjectEquity)
                {
                    if (mEquityTradeBook.ContainsKey(orderRefString))
                        executionPrice = mEquityTradeBook[orderRefString].Price;
                }
            }

            return executionPrice;
        }

    }
    
    public class IciciConstants
    { 
    // String codes corresponding to OrderStatus enum 
        public static string[] OrderStatusString =
        {
        //     QUEUED,
        //REQUESTED,
        //ORDERED,
        //PARTEXEC,
        //EXECUTED,
        //EXPIRED,
        //REJECTED,
        //CANCELLED,
        //PARTEXECANDCANCELLED,
        //NOTFOUND
            "Q",
            "R",
            "O",
            "P",
            "E",
            "X",
            "J",
            "C",
            "C", // Not sure about correctness of this code
            "N"  // Not sure about correctness of this code
        };
        // String codes corresponding to Exchange enum 
        public static string[] ExchangeString =
        {
            "NSE",
            "BSE"
        };
        // String codes corresponding to OrderDirection enum 
        public static string[] OrderDirectionString =
        {
            "B",
            "S"
        };
    }
}