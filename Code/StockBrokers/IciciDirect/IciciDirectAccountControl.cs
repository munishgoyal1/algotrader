using System;
using System.Collections.Generic;
using StockTrader.Core;

namespace StockTrader.Broker.IciciDirect
{
    /////////////////////////////////////////////////
    //////      ICICI ACCOUNT WIDE DATA       ///////
    ///////////////////////////////////////////////// 


    public partial class IciciDirectBroker :
        IBroker,
        IDisposable
    {

        // static ICICI specific brokerage values
        static public readonly double brokerageRate = 0.01;
        static public readonly double minBrokerage = 35;

        // Account specific trading parameters (limits, stock available)
        BrokerAccountMoneyParams accountTradeParams = new BrokerAccountMoneyParams();

        // Locks on global Order & Trade book objects
        object lockObjectEquity = new object();

        // TODO: make them order & trade book objects rather than a dictionary of order infos
        // Order and Trade book instances mapped to order ref strings
        private Dictionary<string, EquityTradeBookRecord> mEquityTradeBook = new Dictionary<string, EquityTradeBookRecord>();
        private Dictionary<string, EquityOrderBookRecord> mEquityOrderBook = new Dictionary<string, EquityOrderBookRecord>();

        // TODO: dummy to be removed later
        List<EquityOrderBookRecord> equityOrders = new List<EquityOrderBookRecord>();
        List<EquityTradeBookRecord> equityTrades = new List<EquityTradeBookRecord>();

        // Dictionary of all stocks being traded in the system currently
        private Dictionary<string, EquityStockTradeStats> mStocksInSystem = new Dictionary<string, EquityStockTradeStats>();

        // Add a stock in the system
        public EquityStockTradeStats AddStockInSystem(string stockCode)
        {
            EquityStockTradeStats stockInfo = new EquityStockTradeStats(stockCode);
            mStocksInSystem.Add(stockCode, stockInfo);
            return stockInfo;
        }

        // Gives out mStocksInSystem
        public Dictionary<string, EquityStockTradeStats> GetStocksInSystem()
        {
            return mStocksInSystem;
        }

        //////////////////////////////////////////////
        //////      ACCOUNT CALCULATIONS       ///////
        //////////////////////////////////////////////  

        public void RefreshAccountPosition()
        {
            accountTradeParams.UpdateTradeParams(GetStocksInSystem());
        }

        public void SetAccountParams(double fundsLimitAtStart, double lossLimit)
        {
            accountTradeParams.FundLimitAtStart = fundsLimitAtStart;
            accountTradeParams.LossLimit = lossLimit;
        }


        // Clear Trade references object
        public void ClearTradeReferenceNumbers(TradingSectionType sectionType)
        {
            if (sectionType.Equals(TradingSectionType.EQUITY))
            {
                lock (lockObjectEquity)
                {
                    mEquityTradeBook.Clear();
                }
            }
        }

        // Clear Order references object
        public void ClearOrderReferenceNumbers(TradingSectionType sectionType)
        {
            if (sectionType.Equals(TradingSectionType.EQUITY))
            {
                lock (lockObjectEquity)
                {
                    mEquityOrderBook.Clear();
                }
            }
        }
    }
}
