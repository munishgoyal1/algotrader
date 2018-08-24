using System;
using System.Collections.Generic;
using StockTrader.Core;

namespace StockTrader.Broker.IciciDirect
{
    public static class IciciUtils
    {
        public static bool HasOrderExecuted(
           IBroker broker,
           string stockCode,
           int quantity,
           double price,
           OrderPriceType orderType,
           OrderDirection orderDirection,
           InstrumentType instrumentType,
           DateTime expiryDate,
           out string orderReferenceNumber)
        {
            orderReferenceNumber = null;

            string contract = String.Empty;
            string strExpDate = expiryDate.ToString("dd-MMM-yyyy");
            if (instrumentType == InstrumentType.FutureIndex || instrumentType == InstrumentType.FutureStock)
            {
                contract += "FUT-";
            }
            else
            {
                throw new NotSupportedException();
            }
            contract += (stockCode + "-" + strExpDate);
            // this loop is there just for potential retry purpose
            for (int count = 1; count <= 2; count++)
            {
                Dictionary<string, DerivativeTradeBookRecord> trades;
                BrokerErrorCode errorCode = broker.GetDerivativesTradeBook(DateTime.Now,
                    DateTime.Now,
                    instrumentType,
                    true,
                    out trades);

                if (!errorCode.Equals(BrokerErrorCode.Success))
                {
                    return false;
                }

                foreach (DerivativeTradeBookRecord singleTrade in trades.Values)
                {
                    if (singleTrade.ContractName.ToUpperInvariant() != contract.ToUpperInvariant())
                    {
                        continue;
                    }
                    if (singleTrade.Quantity != quantity)
                    {
                        continue;
                    }
                    if (singleTrade.Direction != orderDirection)
                    {
                        continue;
                    }
                    orderReferenceNumber = singleTrade.OrderRefenceNumber;
                    return true;
                }
            }
            return false;
        }

       
       
    }
}
