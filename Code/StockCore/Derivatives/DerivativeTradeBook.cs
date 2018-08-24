using System;
using System.Collections;
using System.Data;
using StockTrader.Platform.Database;
using StockTrader.Platform.Logging;

namespace StockTrader.Core
{

    //// Derivative Trade class
    //public class DerivativeTradeBookRecord
    //{
    //    public string ContractName;
    //    public OrderDirection Direction;
    //    public int Quantity;
    //    public double Price;
    //    public string OrderRefenceNumber;
    //    public DateTime TradeDate;
    //    public double TradeValue;
    //    public double Brokerage;
    //}



    // Trade book derivative record info
    public class DerivativeTradeBookRecord
    {
        // Mandatory
        public string ContractName;
        public string UnderlyingSymbol;
        public InstrumentType InstrumentType;
        public OrderDirection Direction;
        public int Quantity;
        public double Price;
        public string OrderRefenceNumber;
        // Parsing for these need to be implemented
        public DateTime TradeDate;
        public double TradeValue;
        public double Brokerage;
        // Custom
        public int AlgoId;
        // Redundant
        public Exchange Exchange = Exchange.NSE;
        // Meta fields
        private bool _isNewTradeRecord;
        public DateTime UpdatedAt; 


        //public DerivativeTradeBookRecord()
        //{
        //    //_isNewTradeRecord = true;
        //}
        public DerivativeTradeBookRecord()
        {
        }

        public DerivativeTradeBookRecord(DerivativeTradeBookRecord dti)
        {
            ContractName = dti.ContractName;
            Direction = dti.Direction;
            Quantity = dti.Quantity;
            Price = dti.Price;
            OrderRefenceNumber = dti.OrderRefenceNumber;

            TradeDate = dti.TradeDate;
            TradeValue = dti.TradeValue;
            Brokerage = dti.Brokerage;

            // Put default update time
            UpdatedAt = DateTime.MaxValue;

            //_isNewTradeRecord = true;
        }


        /// <summary>
        /// Constructor to get an existing TradeRecord
        /// </summary>
        /// <param name="tradeRecordIdToGet">TradeRecord Id for the tradeRecord to get</param>
        /// <param name="userId">UserId Associated with the tradeRecord</param>
        public DerivativeTradeBookRecord(string tradeRecordIdToGet)
        {
            // Gets the TradeRecord detail from database into this tradeRecord's cinstance fields
            bool doesTradeRecordExist = GetTradeRecordFromDatabase(tradeRecordIdToGet);

            // Throws an exception if the tradeRecord does not exisit
            if (!doesTradeRecordExist)
                throw new InvalidOperationException("TradeRecord Id: " + tradeRecordIdToGet + " doesn't exist in the System");
            

            _isNewTradeRecord = false;
        }

        /// <summary>
        /// Update the tradeRecord into database
        /// </summary>
        private void UpdateIntoDatabase()
        {
            DBLibrary dbLib = new DBLibrary();

            dbLib.ExecuteProcedure("sp_upsert_st_derivatives_tradebook", DbParamList(1));
        }

        /// <summary>
        /// Gets the equity trade detail from database
        /// </summary>
        /// <param name="tradeExchangeRef">trade reference to get details for</param>
        /// <returns>true if record found, false otherwise</returns>
        private bool GetTradeRecordFromDatabase(string tradeExchangeRef)
        {
            bool doesTradeRecordExist = false;

            DBLibrary dbLib = new DBLibrary();
            DBUtilities dbUtilities = new DBUtilities();

            try
            {
                ArrayList paramList = new ArrayList
                                          {
                                              dbUtilities.CreateSqlParamater("@orderRef", SqlDbType.VarChar, 50,
                                                                             ParameterDirection.Input, tradeExchangeRef)
                                          };

                // Query the database for an tradeRecord with given tradeRecordId
                DataSet ds = dbLib.ExecuteProcedureDS("sp_select_st_derivatives_tradebook", paramList);

                if (ds != null &&
                    ds.Tables != null &&
                    ds.Tables.Count > 0)
                {
                    DataTable dt = ds.Tables[0];

                    if (dt.Rows.Count > 0)
                    {
                        DataRow dr = dt.Rows[0];

                        // Get StockTradeBookRecord data
                        TradeDate = DateTime.Parse(dr["trade_date"].ToString());
                        ContractName = dr["contract_name"].ToString();
                        Direction = (OrderDirection)Enum.Parse(typeof(OrderDirection), dr["direction"].ToString());
                        Quantity = int.Parse(dr["qty"].ToString());
                        Price = double.Parse(dr["price"].ToString());
                        OrderRefenceNumber = dr["trade_ref"].ToString();
                        Exchange = (Exchange)Enum.Parse(typeof(Exchange), dr["exchange"].ToString());
                        TradeValue = double.Parse(dr["trade_value"].ToString());
                        Brokerage = double.Parse(dr["brokerage"].ToString());
                        AlgoId = int.Parse(dr["algo_id"].ToString());
                        UpdatedAt = DateTime.Parse(dr["status_update_time"].ToString());

                        doesTradeRecordExist = true;
                    }
                }

            }
            catch (Exception ex)
            {
                // If we failed, trace the error for log analysis
                Logger.LogException(ex);
                throw;
            }

            return doesTradeRecordExist;
        }

        /// <summary>
        /// Persists the TradeRecord record in database
        /// </summary>
        private void InsertIntoDatabase()
        {
            // Must be a new tradeRecord to insert, Throw an exception if existing tradeRecord is tried to insert
            if (!_isNewTradeRecord)
                throw new InvalidOperationException("TradeRecord Id: " + OrderRefenceNumber + "already exists in the System");
            
                DBLibrary dbLib = new DBLibrary();

                dbLib.ExecuteProcedure("sp_insert_st_derivatives_tradebook", DbParamList(0));

                // Now it is no more a new tradeRecord as it is entered into the system
                _isNewTradeRecord = false;
            
        }

        /// <summary>
        /// Persist the tradeRecord information into DB
        /// </summary>
        public void Persist()
        {
            UpdateIntoDatabase();
            //// Insert into DB if new tradeRecord, update if tradeRecord already exists in system
            //if (_isNewTradeRecord)
            //{
            //    InsertIntoDatabase();
            //}
            //else
            //{
            //    UpdateIntoDatabase();
            //}
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

            paramList.Add(dbUtilities.CreateSqlParamater("@contractName", SqlDbType.NVarChar, 50, ParameterDirection.Input, this.ContractName));
            paramList.Add(dbUtilities.CreateSqlParamater("@tradeDate", SqlDbType.DateTime, ParameterDirection.Input, this.TradeDate));
            paramList.Add(dbUtilities.CreateSqlParamater("@orderRef", SqlDbType.VarChar, 30, ParameterDirection.Input, this.OrderRefenceNumber));
            paramList.Add(dbUtilities.CreateSqlParamater("@direction", SqlDbType.SmallInt, ParameterDirection.Input, this.Direction));
            paramList.Add(dbUtilities.CreateSqlParamater("@qty", SqlDbType.Int, ParameterDirection.Input, this.Quantity));
            paramList.Add(dbUtilities.CreateSqlParamater("@price", SqlDbType.Decimal, ParameterDirection.Input, this.Price));
            paramList.Add(dbUtilities.CreateSqlParamater("@exchange", SqlDbType.SmallInt, ParameterDirection.Input, this.Exchange));
            paramList.Add(dbUtilities.CreateSqlParamater("@tradeValue", SqlDbType.Decimal, ParameterDirection.Input, this.TradeValue));
            paramList.Add(dbUtilities.CreateSqlParamater("@brokerage", SqlDbType.Decimal, ParameterDirection.Input, this.Brokerage));
            paramList.Add(dbUtilities.CreateSqlParamater("@algoId", SqlDbType.Int, ParameterDirection.Input, this.AlgoId));
            paramList.Add(dbUtilities.CreateSqlParamater("@statusUpdateAt", SqlDbType.VarChar, 127, ParameterDirection.Input, DateTime.Now));
            paramList.Add(dbUtilities.CreateSqlParamater("@upsertStatus", SqlDbType.Bit, ParameterDirection.Output));

            return paramList;
        }


    }
}
