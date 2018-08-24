using System;
using System.Collections.Generic;

namespace StockTrader.Core
{
    //////////////////////////////////////////////
    //////      ACCOUNT FUNDS RELATED       ///////
    //////////////////////////////////////////////  

    public sealed class BrokerAccountMoneyParams : AccountMoneyParams
    {
        // constructor
        public BrokerAccountMoneyParams() { }

        public BrokerAccountMoneyParams(double fundsLimitAtStart, double lossLimit) :
            base(fundsLimitAtStart, lossLimit)
        {

        }

        public void UpdateTradeParams(Dictionary<string, EquityStockTradeStats> stocksInSystem)
        {
            foreach (KeyValuePair<string, EquityStockTradeStats> stockPair in stocksInSystem)
            {
                EquityStockTradeStats stockInfo = stockPair.Value;
                OrderFundLimit += stockInfo.OrderFundLimit;
                FundLimitCurrent += stockInfo.FundLimitCurrent;
                OrderBrokerage += stockInfo.OrderBrokerage;
                TradeBrokerage += stockInfo.TradeBrokerage;
                BuySellGain += stockInfo.BuySellGain;
                NetGain += stockInfo.NetGain;
            }
        }
    }

    public class AccountMoneyParams
    {

        // Constructors
        public AccountMoneyParams()
        {
            ResetBaseValues();
            ResetDynamicValues();
        }

        public AccountMoneyParams(double fundLimitStarting,
            double lossLimit)
        {
            FundLimitAtStart = fundLimitStarting;
            LossLimit = lossLimit;
            ResetDynamicValues();
        }

        protected void ResetBaseValues()
        {
            FundLimitAtStart = 0;
            LossLimit = 0;
        }

        protected void ResetDynamicValues()
        {
            FundLimitCurrent = FundLimitAtStart;
            OrderFundLimit = FundLimitCurrent;
            OrderBrokerage = 0;
            TradeBrokerage = 0;
            BuySellGain = 0;
            NetGain = 0;
        }
        // TODO: seems to be redundant
        protected void TriggerRefreshState()
        {

        }
        object _syncLock = new object();

        // Stock specific current stock limit available for orders
        private double mOrderFundLimit;
        public double OrderFundLimit
        {
            get { lock (_syncLock) { return mOrderFundLimit; } }
            set { lock (_syncLock) { mOrderFundLimit = value; } }
        }

        // Estimated Brokerage fees in current outstanding orders
        private double mOrderBrokerage;
        public double OrderBrokerage
        {
            get { lock (_syncLock) { return mOrderBrokerage; } }
            protected set { lock (_syncLock) { mOrderBrokerage = value; } }
        }

        // Account specific current funds limit remaining after executed trades
        private double mFundLimitCurrent;
        public double FundLimitCurrent
        {
            get { lock (_syncLock) { return mFundLimitCurrent; } }
            protected set { lock (_syncLock) { mFundLimitCurrent = value; } }
        }

        // Net Profit/Loss in day's trade including brokerage
        private double mNetGain;
        public double NetGain
        {
            get { lock (_syncLock) { return mNetGain; } }
            protected set { lock (_syncLock) { mNetGain = value; } }
        }

        // Profit/Loss in day's trade without including brokerage
        private double mBuySellGain;
        public double BuySellGain
        {
            get { lock (_syncLock) { return mBuySellGain; } }
            protected set { lock (_syncLock) { mBuySellGain = value; } }
        }

        // Brokerage fees in day's trade
        private double mTradeBrokerage;
        public double TradeBrokerage
        {
            get { lock (_syncLock) { return mTradeBrokerage; } }
            protected set { lock (_syncLock) { mTradeBrokerage = value; } }
        }

        // Account specific user set limit of loss
        private double mLossLimit;
        public double LossLimit
        {
            get { lock (_syncLock) { return mLossLimit; } }
            set
            {
                // New value cannot be less than what already is negative netgain
                if (NetGain >= 0 ? value >= 0 : value >= Math.Abs(NetGain))
                {

                    lock (_syncLock) { mLossLimit = value; }
                    TriggerRefreshState();
                }
            }
        }


        // Account specific user set limit to use
        private double mFundLimitAtStart;
        public double FundLimitAtStart
        {
            get { lock (_syncLock) { return mFundLimitAtStart; } }
            set
            {
                // Cannot drastically reduce funds limit on the fly, since there may be some 
                // orders lined up or even the limit may already be used in trades
                // First cancel the orders to free up the limit then try to reduce

                // TODO: or automatically try to cancel some buy orders if possible
                // this much intelligence for later phase of project , not now :)
                if (value >= (FundLimitAtStart - FundLimitCurrent))
                {
                    FundLimitCurrent += (value - FundLimitAtStart);
                    lock (_syncLock) { mFundLimitAtStart = value; }
                    TriggerRefreshState();
                }
            }
        }
    }
}
