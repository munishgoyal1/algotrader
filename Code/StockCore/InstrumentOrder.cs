using System;
using StockTrader.Utilities;
using StockTrader.API.TradingAlgos;
using StockTrader.Utilities.Broker;
using System.Threading;
namespace StockTrader.Core
{
    public class Instrument
    {
        public string Symbol;
        public InstrumentType InstrumentType;
        public double StrikePrice;
        public DateTime ExpiryDate;
        public int Qty;

        public Instrument(string symbol, InstrumentType type, int qty)
        {
            Symbol = symbol;
            InstrumentType = type;
            Qty = qty;
        }

        public Instrument(string symbol, InstrumentType type, DateTime expiry, int qty)
            : this(symbol, type, qty)
        {
            ExpiryDate = MarketUtils.GetExpiryExactDate(expiry);
        }

        public Instrument(string symbol, InstrumentType type, double strikePrice, DateTime expiry, int qty)
            :this(symbol, type, expiry, qty)
        {
            StrikePrice = strikePrice;
        }

        public override string ToString()
        {
            if (InstrumentType == InstrumentType.Share)
                return Symbol;

            if (InstrumentType == InstrumentType.FutureIndex || InstrumentType == InstrumentType.FutureStock)
                return "FUT-" + Symbol;
            else
            {
                bool isCall = InstrumentType == InstrumentType.OptionCallIndex || InstrumentType == InstrumentType.OptionCallStock;
                return "OPT-" + (isCall ? "CE" : "PE") + "-" + (int)StrikePrice + "-" + Symbol;
            }
        }

        public string Description()
        {
            string contract = Symbol;

            if (InstrumentType != InstrumentType.Share)
            {
                contract += "-" + ExpiryDate.ToString("dd-MMM-yyyy");
                bool isCall = InstrumentType == InstrumentType.OptionCallIndex || InstrumentType == InstrumentType.OptionCallStock;
                if (InstrumentType == InstrumentType.FutureIndex || InstrumentType == InstrumentType.FutureStock)
                    contract = "FUT-" + contract;
                else if (InstrumentType != InstrumentType.Share)
                    contract = string.Format("{0}-{1}-{2}-{3}-Q{4}", "OPT", contract, (int)StrikePrice, isCall ? "CE" : "PE", Qty);
            }

            return contract;
        }

        public Instrument Clone()
        {
            return new Instrument(Symbol, InstrumentType, StrikePrice, ExpiryDate, Qty);
        }
    }

    [Serializable()]
    public class StockOrder
    {
        public Position OrderPosition = Position.NONE;
        public double OrderPrice;
        public string OrderRef = string.Empty;
        public DTick OrderTick;
        public double OrderProfit;

        // Injected field for debugging. Ideal condition trades calculations
        public double ExpectedOrderPrice;

        public StockOrder(StockOrder stockOrder)
        {
            this.OrderPosition = stockOrder.OrderPosition;
            this.OrderPrice = stockOrder.OrderPrice;
            this.OrderRef = stockOrder.OrderRef;
        }

        public StockOrder(Position orderPosition, double orderPrice)
        {
            OrderPosition = orderPosition;
            OrderPrice = orderPrice;
        }

        public StockOrder(Position orderPosition, double orderPrice, DTick tick)
            :this(orderPosition, orderPrice)
        {
            OrderTick = tick;
        }

        public override string ToString()
        {
            if (OrderPosition == Position.NONE)
                return ("NONE");
            else
                return ("Position=" + OrderPosition + "; Price=" + OrderPrice);
        }
    }
}