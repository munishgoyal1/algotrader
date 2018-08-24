using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using StockTrader.Platform.Database;
using StockTrader.Platform.Logging;
using StockTrader.Utilities;
using StockTrader.Utilities.Broker;

namespace StockTrader.Core
{
    public class SymbolTick
    {
        public DerivativeSymbolTick D;
        public EquitySymbolTick E;
        public Instrument I;
        public double GetLTP()
        {
            if ((I != null && I.InstrumentType != InstrumentType.Share) || (D != null && D.Q != null))
                return D.Q.LastTradedPriceDouble;
            else if (E != null && E.Q != null) return E.Q[0].LastTradePriceDouble;
            else return -1;
        }
        public SymbolTick()
        {
        }
        public SymbolTick(bool allocateD)
        {
            if (allocateD)
                D = new DerivativeSymbolTick();
            else
                E = new EquitySymbolTick();
        }
    }

    [Serializable()]
    public class DerivativeSymbolTick
    {
        public DerivativeSymbolQuote Q;
        public DerivativeSymbolSpread S;
    }
    public class DerivativeSymbolLiveTickGenerator
    {
        public Instrument Instrument;
        protected StreamWriter _ticksw;
        protected readonly string _tickFileName;
        IBroker _broker;
        bool _writeTicks;

        public DerivativeSymbolLiveTickGenerator(IBroker broker, Instrument instrument)
        {
            _broker = broker;
            Instrument = instrument;
            _tickFileName = SystemUtils.GetTickFileName(Instrument.Description());
            _ticksw = new StreamWriter(_tickFileName);
            _ticksw.AutoFlush = true;
        }
        public DerivativeSymbolTick GetTick(bool getSpread, out BrokerErrorCode code)
        {
            DerivativeSymbolQuote Q = null;
            DerivativeSymbolSpread S = null;
            DerivativeSymbolTick T = new DerivativeSymbolTick();
            StringBuilder sb = new StringBuilder();
            code = _broker.GetDerivativeQuote(Instrument.Symbol, Instrument.InstrumentType, Instrument.ExpiryDate, Instrument.StrikePrice, out Q);
            // Check for exchange closed or contract disabled code
            if (code == BrokerErrorCode.Success)
            {
                T.Q = Q;
                sb.Append(Q.ToTickString());
            }
            if (getSpread)
            {
                code = _broker.GetDerivativeSpread(Instrument.Symbol, Instrument.InstrumentType, Instrument.ExpiryDate, Instrument.StrikePrice, out S);
                if (code == BrokerErrorCode.Success)
                {
                    T.S = S;
                    sb.Append(";" + S.ToString());
                }
            }
            if (code.Equals(BrokerErrorCode.Success) && (_writeTicks || MarketUtils.IsTimeTodayAfter915(Q.QuoteTime)))
            {
                _writeTicks = true;
                _ticksw.WriteLine(sb.ToString());
            }
            return T;
        }

        public void Close()
        {
            string backupPath = SystemUtils.GetStockFilesBackupLocation();
            string timeWiseFolder = DateTime.Now.ToString("HHmm");
            string fullBackupPath = backupPath + "\\" + timeWiseFolder + "\\";
            if (!Directory.Exists(fullBackupPath))
                Directory.CreateDirectory(fullBackupPath);
            string bkupTickfile = fullBackupPath + Instrument.Description() + "-Ticks.txt";
            if (File.Exists(_tickFileName) && !File.Exists(bkupTickfile)) File.Copy(_tickFileName, bkupTickfile, false);
            _ticksw.Dispose();
            _ticksw = null;
        }
    }

    [Serializable()]
    public class DerivativeSymbolSpread
    {
        public string Symbol;
        public InstrumentType InstrumentType;
        public DateTime ExpiryDate;
        public double StrikePrice;
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
            }
            sb.Append(Exchange);
            sb.Append(";");
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

    public class DerivativeSymbolQuoteToGet
    {
        public DerivativeSymbolQuoteToGet(string symbol, InstrumentType instrumentType, DateTime expiryDate, double strikePrice)
        {
            Symbol = symbol;
            InstrumentType = instrumentType;
            ExpiryDate = expiryDate;
            StrikePrice = strikePrice;
        }

        public string Symbol;
        public InstrumentType InstrumentType;
        public DateTime ExpiryDate;
        public double StrikePrice;
    }

    // Derivatives
    // TODO: correct naming and placements, code reorg.

    // derivative quote record info
    public class DerivativeSymbolQuoteRecord
    {
        public string ContractName;
        public DateTime QuoteTime;
        public InstrumentType InstrumentType;
        public string AssetUnderlying;
        public double StrikePrice;
        public DateTime ExpiryDate;

        public double LastTradePrice;
        public double AssetPrice;
        public double BidPrice;
        public double OfferPrice;
        public int BidQty;
        public int OfferQty;
        public int TradedQty;

        public Exchange Exchange = Exchange.NSE;

        public DerivativeSymbolQuoteRecord()
        {

        }

        public DerivativeSymbolQuoteRecord(DerivativeSymbolQuote dqi)
        {
            InstrumentType = dqi.InstrumentType;
            AssetUnderlying = dqi.UnderlyingSymbol;
            StrikePrice = dqi.StrikePriceDouble;
            ExpiryDate = DateTime.Parse(dqi.ExpiryDate);

            ContractName = StockUtils.GetInstrumentDescriptionString(InstrumentType, AssetUnderlying, ExpiryDate, StrikePrice);

            QuoteTime = dqi.QuoteTime;
            LastTradePrice = dqi.LastTradedPriceDouble;
            AssetPrice = dqi.AssetPrice;
            BidPrice = dqi.BestBidPriceDouble;
            OfferPrice = dqi.BestOfferPriceDouble;
            BidQty = dqi.BestBidQuantityInt;
            OfferQty = dqi.BestOfferQuantityInt;
            TradedQty = dqi.VolumeTradedInt;
        }


        /// <summary>
        /// Gets the equity order detail from database
        /// </summary>
        /// <param name="orderExchangeRef">order reference to get details for</param>
        /// <returns>true if record found, false otherwise</returns>
        //private bool GetOrderRecordFromDatabase(string orderExchangeRef)
        //{
        //    bool doesOrderRecordExist = false;

        //    DBLibrary dbLib = new DBLibrary();
        //    DBUtilities dbUtilities = new DBUtilities();

        //    try
        //    {
        //        ArrayList paramList = new ArrayList();
        //        paramList.Add(dbUtilities.CreateSqlParamater("@orderRecordId", SqlDbType.VarChar, 50, ParameterDirection.Input, orderExchangeRef));

        //        // Query the database for an orderRecord with given orderRecordId
        //        DataSet ds = dbLib.ExecuteProcedureDS("sp_select_pdr_orderRecord", paramList);

        //        if (ds != null &&
        //            ds.Tables != null &&
        //            ds.Tables.Count > 0)
        //        {
        //            DataTable dt = ds.Tables[0];

        //            if (dt.Rows.Count > 0)
        //            {
        //                DataRow dr = dt.Rows[0];

        //                // Get StockOrderBookRecord data
        //                Date = DateTime.Parse(dr["date"].ToString());
        //                ContractName = dr["contract"].ToString();
        //                Direction = (OrderDirection)Enum.Parse(typeof(OrderDirection), dr["direction"].ToString());
        //                Quantity = int.Parse(dr["qty"].ToString());
        //                Price = double.Parse(dr["price"].ToString());
        //                OrderRefenceNumber = dr["order_ref"].ToString();
        //                Exchange = (Exchange)Enum.Parse(typeof(Exchange), dr["exchange"].ToString());
        //                OrderStatus = (OrderStatus)Enum.Parse(typeof(OrderStatus), dr["status"].ToString());
        //                OpenQty = int.Parse(dr["qty_open"].ToString());
        //                ExecutedQty = int.Parse(dr["qty_executed"].ToString());
        //                ExpiredQty = int.Parse(dr["qty_expired"].ToString());
        //                CancelledQty = int.Parse(dr["qty_cancelled"].ToString());
        //                StopLossPrice = double.Parse(dr["stoploss_price"].ToString());
        //                UpdatedAt = DateTime.Parse(dr["status_update_time"].ToString());
        //                AlgoId = int.Parse(dr["algo_id"].ToString());

        //                doesOrderRecordExist = true;
        //            }
        //        }

        //    }
        //    catch (Exception ex)
        //    {
        //        // If we failed, trace the error for log analysis
        //        Logger.LogException(ex);
        //        throw;
        //    }

        //    return doesOrderRecordExist;
        //}

        /// <summary>
        /// Persists the OrderRecord record in database
        /// </summary>
        private void InsertIntoDatabase()
        {
            DBLibrary dbLib = new DBLibrary();

            dbLib.ExecuteProcedure("sp_insert_st_derivatives_quote", DbParamList(0));
        }

        /// <summary>
        /// Persist the orderRecord information into DB
        /// </summary>
        public void Persist()
        {
            InsertIntoDatabase();
        }

        /// <summary>
        /// Method to prepare the Database parameter list
        /// </summary>
        /// <param name="cmdType">Command Type</param>
        /// <returns>Arraylist of SQL parameters</returns>
        public ArrayList DbParamList(int cmdType)
        {
            ArrayList paramList = new ArrayList();

            DBUtilities dbUtilities = new DBUtilities();

            paramList.Add(dbUtilities.CreateSqlParamater("@contractName", SqlDbType.NVarChar, 100, ParameterDirection.Input, this.ContractName));
            paramList.Add(dbUtilities.CreateSqlParamater("@quoteTime", SqlDbType.DateTime, ParameterDirection.Input, this.QuoteTime));
            paramList.Add(dbUtilities.CreateSqlParamater("@instrumentType", SqlDbType.SmallInt, ParameterDirection.Input, this.InstrumentType));
            paramList.Add(dbUtilities.CreateSqlParamater("@assetUnderlying", SqlDbType.VarChar, 50, ParameterDirection.Input, this.AssetUnderlying));
            paramList.Add(dbUtilities.CreateSqlParamater("@strikePrice", SqlDbType.Decimal, ParameterDirection.Input, this.StrikePrice));
            paramList.Add(dbUtilities.CreateSqlParamater("@expiryDate", SqlDbType.DateTime, ParameterDirection.Input, this.ExpiryDate));

            paramList.Add(dbUtilities.CreateSqlParamater("@lastTradePrice", SqlDbType.Decimal, ParameterDirection.Input, this.LastTradePrice));
            paramList.Add(dbUtilities.CreateSqlParamater("@assetPrice", SqlDbType.Decimal, ParameterDirection.Input, this.AssetPrice));
            paramList.Add(dbUtilities.CreateSqlParamater("@bidPrice", SqlDbType.Decimal, ParameterDirection.Input, this.BidPrice));
            paramList.Add(dbUtilities.CreateSqlParamater("@offerPrice", SqlDbType.Decimal, ParameterDirection.Input, this.OfferPrice));

            paramList.Add(dbUtilities.CreateSqlParamater("@bidQty", SqlDbType.Int, ParameterDirection.Input, this.BidQty));
            paramList.Add(dbUtilities.CreateSqlParamater("@offerQty", SqlDbType.Int, ParameterDirection.Input, this.OfferQty));
            paramList.Add(dbUtilities.CreateSqlParamater("@tradedQty", SqlDbType.Int, ParameterDirection.Input, this.TradedQty));
            paramList.Add(dbUtilities.CreateSqlParamater("@exchange", SqlDbType.SmallInt, ParameterDirection.Input, this.Exchange));

            switch (cmdType)
            {
                case 0:
                    paramList.Add(dbUtilities.CreateSqlParamater("@insertStatus", SqlDbType.Bit, ParameterDirection.Output));
                    break;
                case 1:
                    paramList.Add(dbUtilities.CreateSqlParamater("@updateStatus", SqlDbType.Bit, ParameterDirection.Output));
                    break;
            }

            return paramList;
        }
    }

    [Serializable()]
    public sealed class DerivativeSymbolQuote
    {
        public DerivativeSymbolQuote()
        {
            UpdateTime = DateTime.Now;
        }

        public DerivativeSymbolQuote(double ltp, double bidPrice, double offerPrice)
        {
            LastTradedPriceDouble = ltp;
            UpdateTime = DateTime.Now;
            BestOfferPriceDouble = offerPrice;
            BestBidPriceDouble = bidPrice;
        }
        public DerivativeSymbolQuote(double ltp, double bidPrice, double offerPrice, int buyQty, int sellQty)
        {
            UpdateTime = DateTime.Now;
            LastTradedPriceDouble = ltp;
            BestOfferPriceDouble = offerPrice;
            BestBidPriceDouble = bidPrice;
            BestBidQuantityInt = buyQty;
            BestOfferQuantityInt = sellQty;
        }
        public DerivativeSymbolQuote(DateTime tickTime, double ltp, double bidPrice,
            double offerPrice, int buyQty, int sellQty, int volume)
        {
            LastTradedPriceDouble = ltp;
            UpdateTime = tickTime;
            BestOfferPriceDouble = offerPrice;
            BestBidPriceDouble = bidPrice;
            QuoteTime = tickTime;
            VolumeTradedInt = volume;
            BestBidQuantityInt = buyQty;
            BestOfferQuantityInt = sellQty;
        }

        public double AssetPrice;
        public DateTime QuoteTime = DateTime.Now;
        public DateTime UpdateTime;
        public string UnderlyingSymbol;
        public double DayOpenPrice;
        public double DayHighPrice;
        public double DayLowPrice;
        public double PrevClosePrice;
        public double PercChangeFromPrevious;

        public DateTime ExpiryDateTime;
        private string mExpiryDate;
        /// <summary>
        /// The ExpiryDate should be in ddMMMyyyy format
        /// </summary>
        public string ExpiryDate
        {
            get { return mExpiryDate; }
            set
            {
                mExpiryDate = value;
                ExpiryDateTime = DateTime.ParseExact(mExpiryDate, "ddMMMyyyy", DateTimeFormatInfo.InvariantInfo);
            }
        }

        public InstrumentType InstrumentType;
        public int TickNumber;
        public double StrikePriceDouble;
        public double LastTradedPriceDouble;
        public int VolumeTradedInt;
        public double BestBidPriceDouble;
        public int BestBidQuantityInt;
        public double BestOfferPriceDouble;
        public int BestOfferQuantityInt;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("InstrumentType=" + InstrumentType);
            sb.AppendLine("Underlying=" + UnderlyingSymbol);
            sb.AppendLine("ExpiryDate=" + mExpiryDate);
            sb.AppendLine("StrikePrice=" + StrikePriceDouble);
            sb.AppendLine("LastTradedPrice=" + LastTradedPriceDouble);
            sb.AppendLine("BidPrice=" + BestBidPriceDouble);
            sb.AppendLine("OfferPrice=" + BestOfferPriceDouble);
            sb.AppendLine();
            return sb.ToString();
        }

        public string ToTickString()
        {
            var sb = new StringBuilder();
            sb.Append(QuoteTime.ToString("yyyyMMdd:HH:mm:ss")); sb.Append(";");
            sb.Append(LastTradedPriceDouble); sb.Append(";");
            sb.Append(BestBidPriceDouble); sb.Append(";");
            sb.Append(BestOfferPriceDouble); sb.Append(";");
            sb.Append(BestBidQuantityInt); sb.Append(";");
            sb.Append(BestOfferQuantityInt); sb.Append(";");
            sb.Append(VolumeTradedInt); sb.Append(";");
            return sb.ToString();
        }
    }

    // OLD- not used currently contains string represenations for every field. then parsing typed field from string.
    public sealed class DerivativeSymbolQuote1 : IComparable<DerivativeSymbolQuote1>, IComparer<DerivativeSymbolQuote1>
    {
        public DerivativeSymbolQuote1()
        {
            UpdateTime = DateTime.Now;
        }

        public DerivativeSymbolQuote1(double lastTradedPrice, double bidPrice, double offerPrice)
        {
            UpdateTime = DateTime.Now;
            LastTradedPrice = lastTradedPrice.ToString("F2");
            BidPrice = bidPrice.ToString("F2"); ;
            OfferPrice = offerPrice.ToString("F2");
        }
        public DerivativeSymbolQuote1(double underlyingValue, double buyPrice, double sellPrice, int buyQty, int sellQty)
        {
            UpdateTime = DateTime.Now;
            LastTradedPrice = underlyingValue.ToString("F2");
            BidPrice = buyPrice.ToString("F2"); ;
            OfferPrice = sellPrice.ToString("F2");
            BidQuantity = buyQty.ToString(NumberFormatInfo.InvariantInfo);
            OfferQuantity = sellQty.ToString(NumberFormatInfo.InvariantInfo);
        }
        public DerivativeSymbolQuote1(DateTime tickTime, double underlyingValue, double buyPrice,
            double sellPrice, int buyQty, int sellQty, int volume)
        {
            //LastTradedPrice = underlyingValue.ToString("F2");
            //BidPrice = buyPrice.ToString("F2"); ;
            //OfferPrice = sellPrice.ToString("F2");
            //BidQuantity = buyQty.ToString(NumberFormatInfo.InvariantInfo);
            //OfferQuantity = sellQty.ToString(NumberFormatInfo.InvariantInfo);
            //VolumeTraded = volume.ToString(NumberFormatInfo.InvariantInfo);
            //UpdateTime = tickTime;
            //OriginalTime = tickTime;

            mLastTradedPriceDouble = underlyingValue;
            UpdateTime = tickTime;
            mOfferPriceDouble = sellPrice;
            mBidPriceDouble = buyPrice;
            QuoteTime = tickTime;
            VolumeTradedInt = volume;
        }

        public double AssetPrice;

        public DateTime QuoteTime = DateTime.Now;

        public DateTime UpdateTime { get; set; }

        public string UnderlyingSymbol { get; set; }


        public double DayOpenPrice;
        public double DayHighPrice;
        public double DayLowPrice;
        public double PrevClosePrice;

        private DateTime mExpiryDateTime;
        private string mExpiryDate;
        /// <summary>
        /// The ExpiryDate should be in ddMMMyyyy format
        /// </summary>
        public string ExpiryDate
        {
            get { return mExpiryDate; }
            set
            {
                mExpiryDate = value;
                mExpiryDateTime = DateTime.ParseExact(mExpiryDate, "ddMMMyyyy", DateTimeFormatInfo.InvariantInfo);
            }
        }

        public InstrumentType InstrumentType { get; set; }

        public int TickNumber { get; set; }

        private double mStrikePriceDouble;
        public double StrikePriceDouble
        {
            get
            {
                return mStrikePriceDouble;
            }
        }
        private string mStrikePrice;
        public string StrikePrice
        {
            set
            {
                mStrikePrice = value;
                mStrikePriceDouble = 0;
                double.TryParse(mStrikePrice, out mStrikePriceDouble);
                Calculate();
            }
        }

        public double ProbableNextTradeValue { get; private set; }

        private double mLastTradedPriceDouble;
        public double LastTradedPriceDouble
        {
            get
            {
                return mLastTradedPriceDouble;
            }
            set { mLastTradedPriceDouble = value; }  // BUGBUG: temp. addition for quick file generations, revisit later
        }

        public int VolumeTradedInt { get; private set; }

        private string mVolumeTraded;
        public string VolumeTraded
        {
            set
            {
                mVolumeTraded = value;
                double temp;
                double.TryParse(mVolumeTraded, out temp);
                VolumeTradedInt = (int)temp;
            }
        }

        private string mLastTradedPrice;
        public string LastTradedPrice
        {
            set
            {
                mLastTradedPrice = value;
                mLastTradedPriceDouble = 0;
                mLastTradedPriceDouble = double.Parse(mLastTradedPrice);
                Calculate();
            }
        }

        private double mBidPriceDouble;
        public double BestBidPriceDouble
        {
            get
            {
                return mBidPriceDouble;
            }
        }
        private string mBidPrice;
        public string BidPrice
        {
            set
            {
                mBidPrice = value;
                mBidPriceDouble = 0;
                double.TryParse(mBidPrice, out mBidPriceDouble);
            }
        }

        private int mBidQuantityInt;
        public int BestBidQuantityInt
        {
            get
            {
                return mBidQuantityInt;
            }
        }
        private string mBidQuantity;
        public string BidQuantity
        {
            set
            {
                mBidQuantity = value;
                if (!string.IsNullOrEmpty(mBidQuantity))
                {
                    mBidQuantity = mBidQuantity.Replace(",", String.Empty);
                }
                mBidQuantityInt = 0;
                int.TryParse(mBidQuantity, out mBidQuantityInt);
                Calculate();
            }
        }

        private double mOfferPriceDouble;
        public double BestOfferPriceDouble
        {
            get
            {
                return mOfferPriceDouble;
            }
        }
        private string mOfferPrice;
        public string OfferPrice
        {
            set
            {
                mOfferPrice = value;
                mOfferPriceDouble = 0;
                double.TryParse(mOfferPrice, out mOfferPriceDouble);
                Calculate();
            }
        }


        private int mOfferQuantityInt;
        public int BestOfferQuantityInt
        {
            get
            {
                return mOfferQuantityInt;
            }
        }
        private string mOfferQuantity;
        public string OfferQuantity
        {
            set
            {
                mOfferQuantity = value;
                if (!string.IsNullOrEmpty(mOfferQuantity))
                {
                    mOfferQuantity = mOfferQuantity.Replace(",", String.Empty);
                }
                mOfferQuantityInt = 0;
                int.TryParse(mOfferQuantity, out mOfferQuantityInt);
                Calculate();
            }
        }

        public double CallBreakEvenAtRiseFall_ForBuy { get; private set; }

        private void Calculate()
        {
            //MUNISHTOREMOVE
            return;
            #region mCallBreakEvenAtRiseFall_ForBuy
            {
                if (mOfferPriceDouble == 0 || mLastTradedPriceDouble == 0)
                {
                    CallBreakEvenAtRiseFall_ForBuy = -1;
                }
                else
                {
                    if (InstrumentType == InstrumentType.OptionCallIndex ||
                        InstrumentType == InstrumentType.OptionPutIndex ||
                        InstrumentType == InstrumentType.OptionCallStock ||
                        InstrumentType == InstrumentType.OptionPutStock)
                    {
                        if (mStrikePriceDouble != 0)
                        {
                            if (InstrumentType == InstrumentType.OptionCallIndex ||
                                InstrumentType == InstrumentType.OptionCallStock)
                            {
                                CallBreakEvenAtRiseFall_ForBuy = (mStrikePriceDouble + mOfferPriceDouble - mLastTradedPriceDouble);
                            }
                            else if (InstrumentType == InstrumentType.OptionPutIndex ||
                                InstrumentType == InstrumentType.OptionPutStock)
                            {
                                CallBreakEvenAtRiseFall_ForBuy = (mLastTradedPriceDouble - (mStrikePriceDouble - mOfferPriceDouble));
                            }
                        }
                        else
                        {
                            CallBreakEvenAtRiseFall_ForBuy = -1;
                        }
                    }
                    else if (InstrumentType == InstrumentType.FutureIndex ||
                        InstrumentType == InstrumentType.FutureStock)
                    {
                        CallBreakEvenAtRiseFall_ForBuy = (mOfferPriceDouble - mLastTradedPriceDouble);
                    }
                    else
                    {
                        CallBreakEvenAtRiseFall_ForBuy = -1;
                    }
                }
            }
            #endregion

            #region mProbableNextTradeValue
            {
                if (mBidQuantityInt == 0 && mOfferQuantityInt == 0)
                {
                    ProbableNextTradeValue = -1;
                }
                else
                {
                    double totVal = 0;
                    int totQty = 0;
                    totVal += (mBidQuantityInt * mOfferPriceDouble);
                    totVal += (mOfferQuantityInt * mBidPriceDouble);
                    totQty = (mBidQuantityInt + mOfferQuantityInt);
                    ProbableNextTradeValue = totVal / totQty;
                    decimal price = (decimal)ProbableNextTradeValue;
                    price = decimal.Round(price, 2);
                    ProbableNextTradeValue = decimal.ToDouble(price);
                }
            }
            #endregion
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("InstrumentType=" + InstrumentType);
            sb.AppendLine("Underlying=" + UnderlyingSymbol);
            sb.AppendLine("ExpiryDate=" + mExpiryDate);
            sb.AppendLine("StrikePrice=" + mStrikePrice);
            sb.AppendLine("LastTradedPrice=" + mLastTradedPrice);
            sb.AppendLine("BidPrice=" + mBidPrice);
            sb.AppendLine("OfferPrice=" + mOfferPrice);
            sb.AppendLine();
            return sb.ToString();
        }

        public string ToTickString()
        {
            var sb = new StringBuilder();
            sb.Append(QuoteTime.ToString("yyyyMMdd:HH:mm:ss")); sb.Append(";");
            sb.Append(LastTradedPriceDouble); sb.Append(";");
            sb.Append(BestBidPriceDouble); sb.Append(";");
            sb.Append(BestOfferPriceDouble); sb.Append(";");
            sb.Append(BestBidQuantityInt); sb.Append(";");
            sb.Append(BestOfferQuantityInt); sb.Append(";");
            sb.Append(VolumeTradedInt); sb.Append(";");
            return sb.ToString();
        }

        #region IComparable<DerivativeSymbolQuote1Record> Members

        public int CompareTo(DerivativeSymbolQuote1 other)
        {
            int retVal = this.InstrumentType.CompareTo(other.InstrumentType);
            if (retVal != 0) return retVal;

            retVal = this.mExpiryDateTime.CompareTo(other.mExpiryDateTime);
            if (retVal != 0) return retVal;

            if (this.CallBreakEvenAtRiseFall_ForBuy != 0 && other.CallBreakEvenAtRiseFall_ForBuy != 0)
            {
                retVal = this.CallBreakEvenAtRiseFall_ForBuy.CompareTo(other.CallBreakEvenAtRiseFall_ForBuy);
            }
            if (retVal != 0) return retVal;

            retVal = this.mOfferPrice.CompareTo(other.mOfferPrice);
            if (retVal != 0) return retVal;

            return 0;
        }

        #endregion

        #region IComparer<DerivativeSymbolQuote1Record> Members

        public int Compare(DerivativeSymbolQuote1 x, DerivativeSymbolQuote1 y)
        {
            return x.CompareTo(y);
        }

        #endregion
    }
}
