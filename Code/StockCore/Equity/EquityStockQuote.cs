using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using StockTrader.Platform.Logging;
using StockTrader.Utilities;

namespace StockTrader.Core
{
    [Serializable()]
    public sealed class EquitySymbolQuote : IComparable<EquitySymbolQuote>, IComparer<EquitySymbolQuote>
    {
        // Stock code
        public string StockCode { get; set; }

        // Exchange to deal with
        public Exchange Exchange { get; set; }

        // Last updated time
        public DateTime QuoteTime { get; set; }

        //public EquityStockSpread Spread;

        // Price Info //
        public double LowerCircuitPrice;
        public double UpperCircuitPrice;
        public string ExchangeStr;

        public double ATP;


        // LTP
        private double mLastTradePriceDouble;
        public double LastTradePriceDouble
        {
            get
            {
                return mLastTradePriceDouble;
            }
        }
        private string mLastTradePrice;
        public string LastTradePrice
        {
            set
            {
                mLastTradePrice = value;
                //mLastTradePriceDouble = 0;
                mLastTradePriceDouble = double.Parse(mLastTradePrice);
            }
        }

        // Day Open Price
        private double mOpenPriceDouble;
        public double OpenPriceDouble
        {
            get
            {
                return mOpenPriceDouble;
            }
        }
        private string mOpenPrice;
        public string OpenPrice
        {
            set
            {
                mOpenPrice = value;
                //mOpenPriceDouble = 0;
                double.TryParse(mOpenPrice, out mOpenPriceDouble);
            }
        }

        // Day High Price
        private double mHighPriceDouble;
        public double HighPriceDouble
        {
            get
            {
                return mHighPriceDouble;
            }
        }
        private string mHighPrice;
        public string HighPrice
        {
            set
            {
                mHighPrice = value;
                //mHighPriceDouble = 0;
                double.TryParse(mHighPrice, out mHighPriceDouble);
            }
        }

        // Day Low Price
        private double mLowPriceDouble;
        public double LowPriceDouble
        {
            get
            {
                return mLowPriceDouble;
            }
        }
        private string mLowPrice;
        public string LowPrice
        {
            set
            {
                mLowPrice = value;
                //mLowPriceDouble = 0;
                double.TryParse(mLowPrice, out mLowPriceDouble);
            }
        }

        public double ClosePrice;
        // Previous Day Close Price
        private double mPreviousClosePriceDouble;
        public double PreviousClosePriceDouble
        {
            get
            {
                return mPreviousClosePriceDouble;
            }
        }
        private string mPreviousClosePrice;
        public string PreviousClosePrice
        {
            set
            {
                mPreviousClosePrice = value;
                //mPreviousClosePriceDouble = 0;
                double.TryParse(mPreviousClosePrice, out mPreviousClosePriceDouble);
            }
        }

        // Percentage Change
        private double mPercentageChangeDouble;
        public double PercentageChangeDouble
        {
            get
            {
                return mPercentageChangeDouble;
            }
        }
        private string mPercentageChange;
        public string PercentageChange
        {
            set
            {
                mPercentageChange = value;
                mPercentageChangeDouble = double.Parse(mPercentageChange);
            }
        }

        // Volume
        private int mVolumeTradedInt;
        public int VolumeTradedInt
        {
            get { return mVolumeTradedInt; }
        }

        private string mVolumeTraded;
        public string VolumeTraded
        {
            set
            {
                mVolumeTraded = value;
                double temp;
                double.TryParse(mVolumeTraded, out temp);
                mVolumeTradedInt = (int)temp;
            }
        }

        // Best Bid & Offer info

        // Best Bid Price
        private double mBestBidPriceDouble;
        public double BestBidPriceDouble
        {
            get
            {
                return mBestBidPriceDouble;
            }
        }
        private string mBestBidPrice;
        public string BestBidPrice
        {
            set
            {
                mBestBidPrice = value;
                //mBestBidPriceDouble = 0;
                if (mBestBidPrice != "NA")
                    double.TryParse(mBestBidPrice, out mBestBidPriceDouble);
            }
        }

        // Best Offer Price
        private double mBestOfferPriceDouble;
        public double BestOfferPriceDouble
        {
            get
            {
                return mBestOfferPriceDouble;
            }
        }
        private string mBestOfferPrice;
        public string BestOfferPrice
        {
            set
            {
                mBestOfferPrice = value;
                //mBestOfferPriceDouble = 0;
                if (mBestOfferPrice != "NA")
                    mBestOfferPriceDouble = double.Parse(mBestOfferPrice);
            }
        }


        // Best Bid Qty
        private int mBestBidQtyInt;
        public int BestBidQtyInt
        {
            get { return mBestBidQtyInt; }
        }

        private string mBestBidQty;
        public string BestBidQty
        {
            set
            {
                mBestBidQty = value;
                if (mBestBidQty != "NA")
                {
                    double temp;
                    double.TryParse(mBestBidQty, out temp);
                    mBestBidQtyInt = (int)temp;
                }
            }
        }

        // Best Offer Qty
        private int mBestOfferQtyInt;
        public int BestOfferQtyInt
        {
            get { return mBestOfferQtyInt; }
        }

        private string mBestOfferQty;
        public string BestOfferQty
        {
            set
            {
                mBestOfferQty = value;
                if (mBestOfferQty != "NA")
                {
                    double temp;
                    double.TryParse(mBestOfferQty, out temp);
                    mBestOfferQtyInt = (int)temp;
                }
            }
        }

        // Best Bid Price
        private double mPrice52WkHighDouble;
        public double Price52WkHighDouble
        {
            get
            {
                return mPrice52WkHighDouble;
            }
        }
        private string mPrice52WkHigh;
        public string Price52WkHigh
        {
            set
            {
                mPrice52WkHigh = value;
                //mPrice52WkHighDouble = 0;
                double.TryParse(mPrice52WkHigh, out mPrice52WkHighDouble);
            }
        }

        // Best Offer Price
        private double mPrice52WkLowDouble;
        public double Price52WkLowDouble
        {
            get
            {
                return mPrice52WkLowDouble;
            }
        }
        private string mPrice52WkLow;
        public string Price52WkLow
        {
            set
            {
                mPrice52WkLow = value;
                //mPrice52WkLowDouble = 0;
                double.TryParse(mPrice52WkLow, out mPrice52WkLowDouble);
            }
        }

        public string ToTickString()
        {
            var sb = new StringBuilder();
            sb.Append(QuoteTime.ToString("yyyyMMdd:HH:mm:ss")); sb.Append(";");
            sb.Append(LastTradePriceDouble); sb.Append(";");
            sb.Append(BestBidPriceDouble); sb.Append(";");
            sb.Append(BestOfferPriceDouble); sb.Append(";");
            sb.Append(BestBidQtyInt); sb.Append(";");
            sb.Append(BestOfferQtyInt); sb.Append(";");
            sb.Append(VolumeTradedInt); sb.Append(";");
            return sb.ToString();
        }

        // TODO: implement proper comparison when required
        #region IComparable<EquityStockQuote> Members

        public int CompareTo(EquitySymbolQuote other)
        {
            int retVal = this.BestBidPriceDouble.CompareTo(other.BestBidPriceDouble);
            if (retVal != 0) return retVal;

            retVal = this.BestBidPriceDouble.CompareTo(other.BestBidPriceDouble);
            if (retVal != 0) return retVal;


            return 0;
        }

        #endregion

        #region IComparer<EquityStockQuote> Members

        public int Compare(EquitySymbolQuote x, EquitySymbolQuote y)
        {
            return x.CompareTo(y);
        }

        #endregion

    }

    [Serializable()]
    public class EquitySymbolSpread
    {
        public string Symbol;
        public DateTime QuoteTime = DateTime.Now;
        public Exchange Exchange;

        public double[] BestBidPrice = new double[5];
        public int[] BestBidQty = new int[5];
        public double[] BestOfferPrice = new double[5];
        public int[] BestOfferQty = new int[5];

        public int TotalBidQty;
        public int TotalOfferQty;

        public string ToString(bool includeMetaInfo)
        {
            StringBuilder sb = new StringBuilder();
            if (includeMetaInfo)
            {
                sb.Append(QuoteTime.ToString("yyyyMMdd:HH:mm:ss")); sb.Append(";");
                //sb.Append(Symbol); sb.Append(";");
                sb.Append(Exchange); sb.Append(";");
            }
            sb.Append(string.Join(";", BestBidPrice));
            sb.Append(";");
            sb.Append(string.Join(";", BestBidQty));
            sb.Append(";");
            sb.Append(string.Join(";", BestOfferPrice));
            sb.Append(";");
            sb.Append(TotalBidQty);
            sb.Append(";");
            sb.Append(TotalOfferQty);
            return sb.ToString();
        }
    }

    [Serializable()]
    public class EquitySymbolTick
    {
        public EquitySymbolQuote[] Q;
        public EquitySymbolSpread[] S;
    }
}
