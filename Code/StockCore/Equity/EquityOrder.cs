using System;

namespace StockTrader.Core
{
    // Equity Related
    public class EquityOrder
    {
        public readonly String StockCode;
        public readonly int Quantity;
        public readonly String Price;   // for delivery order
        public readonly OrderPriceType OrderPriceType;
        public readonly OrderDirection OrderDirection;
        public readonly Exchange Exchange;
        public readonly EquityOrderType EqOrderType;
        // for margin orders
        public readonly String StopLossPrice;
        public readonly String LimitPrice;


        public EquityOrder(String StockCode,
            int Quantity,
            String Price,
            OrderPriceType orderPriceType,
            OrderDirection orderDirection,
            Exchange exchange,
            EquityOrderType eqOrderType,
            String stopLossPrice,
            String limitPrice)
        {
            this.StockCode = StockCode;
            this.Quantity = Quantity;
            this.Price = Price;
            this.OrderPriceType = orderPriceType;
            this.OrderDirection = orderDirection;
            this.Exchange = exchange;
            this.EqOrderType = eqOrderType;
            this.StopLossPrice = stopLossPrice;
            this.LimitPrice = limitPrice;
        }
    }

}
