using System;
using System.Collections;
using System.Data;
using StockTrader.Platform.Database;
using StockTrader.Platform.Logging;

namespace StockTrader.Core
{
    // Derivative order class
    //public class DerivativeOrderBookRecord
    //{
    //    public string ContractName;
    //    public OrderDirection Direction;
    //    public int Quantity;
    //    public double Price;
    //    public string OrderRefenceNumber;
    //    public OrderStatus OrderStatus;
    //    public DateTime OrderDate;
    //    public int OpenQty;
    //    public int ExecutedQty;
    //    public int ExpiredQty;
    //    public int CancelledQty;
    //    public double StoplossPrice;
    //}



    // Order book derivative record info
    public class DerivativeOrderBookRecord
    {
        public string ContractName;
        public string UnderlyingSymbol;
        public InstrumentType InstrumentType;
        public OrderDirection Direction;
        public int Quantity;
        public double Price;
        public string OrderRefenceNumber;
        public OrderStatus OrderStatus;
        public DateTime OrderDate;
        public Exchange Exchange = Exchange.NSE;
        // Custom
        public int AlgoId;
        public int OpenQty;
        public int ExecutedQty;
        public int ExpiredQty;
        public int CancelledQty;
        public double StopLossPrice;

        // Meta fields
        private bool _isNewOrderRecord;
        public DateTime UpdatedAt;

        //public DerivativeOrderBookRecord()
        //{
        //    //_isNewOrderRecord = true;
        //}

        //public DerivativeOrderBookRecord(string orderRefenceNumber)
        //{
        //    OrderRefenceNumber = orderRefenceNumber;
        //    GetOrderRecordFromDatabase(orderRefenceNumber);
        //    _isNewOrderRecord = false;
        //}

        public DerivativeOrderBookRecord()
        {

        }

        public DerivativeOrderBookRecord(DerivativeOrderBookRecord doi)
        {
            ContractName = doi.ContractName;
            Direction = doi.Direction;
            Quantity = doi.Quantity;
            Price = doi.Price;
            OrderRefenceNumber = doi.OrderRefenceNumber;
            OrderStatus = doi.OrderStatus;

            OrderDate = doi.OrderDate;
            OpenQty = doi.OpenQty;
            ExecutedQty = doi.ExecutedQty;
            CancelledQty = doi.CancelledQty;
            ExpiredQty = doi.ExpiredQty;
            StopLossPrice = doi.StopLossPrice;

            // Put default update time
            UpdatedAt = DateTime.MaxValue;

            //_isNewOrderRecord = true;
        }

        /// <summary>
        /// Constructor to get an existing OrderRecord
        /// </summary>
        /// <param name="orderRecordIdToGet">OrderRecord Id for the orderRecord to get</param>
        /// <param name="userId">UserId Associated with the orderRecord</param>
        public DerivativeOrderBookRecord(string orderRecordIdToGet)
        {
            // Gets the OrderRecord detail from database into this orderRecord's cinstance fields
            bool doesOrderRecordExist = GetOrderRecordFromDatabase(orderRecordIdToGet);

            // Throws an exception if the orderRecord does not exisit
            if (!doesOrderRecordExist)
            {
                throw new System.InvalidOperationException("OrderRecord Id: " + orderRecordIdToGet + " doesn't exist in the System");
            }

            //_isNewOrderRecord = false;
        }

        /// <summary>
        /// Update the orderRecord into database
        /// </summary>
        private void UpdateIntoDatabase()
        {
            DBLibrary dbLib = new DBLibrary();

            dbLib.ExecuteProcedure("sp_upsert_st_derivatives_orderbook", DbParamList(1));
        }

        /// <summary>
        /// Gets the equity order detail from database
        /// </summary>
        /// <param name="orderExchangeRef">order reference to get details for</param>
        /// <returns>true if record found, false otherwise</returns>
        private bool GetOrderRecordFromDatabase(string orderExchangeRef)
        {
            bool doesOrderRecordExist = false;

            DBLibrary dbLib = new DBLibrary();
            DBUtilities dbUtilities = new DBUtilities();

            try
            {
                ArrayList paramList = new ArrayList
                                          {
                                              dbUtilities.CreateSqlParamater("@orderRef", SqlDbType.VarChar, 50,
                                                                             ParameterDirection.Input, orderExchangeRef)
                                          };

                // Query the database for an orderRecord with given orderRecordId
                DataSet ds = dbLib.ExecuteProcedureDS("sp_select_st_derivatives_orderbook", paramList);

                if (ds != null &&
                    ds.Tables != null &&
                    ds.Tables.Count > 0)
                {
                    DataTable dt = ds.Tables[0];

                    if (dt.Rows.Count > 0)
                    {
                        DataRow dr = dt.Rows[0];

                        // Get StockOrderBookRecord data
                        OrderDate = DateTime.Parse(dr["order_date"].ToString());
                        ContractName = dr["contract_name"].ToString();
                        Direction = (OrderDirection)Enum.Parse(typeof(OrderDirection), dr["direction"].ToString());
                        Quantity = int.Parse(dr["qty"].ToString());
                        Price = double.Parse(dr["price"].ToString());
                        OrderRefenceNumber = dr["order_ref"].ToString();
                        Exchange = (Exchange)Enum.Parse(typeof(Exchange), dr["exchange"].ToString());
                        OrderStatus = (OrderStatus)Enum.Parse(typeof(OrderStatus), dr["order_status"].ToString());
                        OpenQty = int.Parse(dr["qty_open"].ToString());
                        ExecutedQty = int.Parse(dr["qty_executed"].ToString());
                        ExpiredQty = int.Parse(dr["qty_expired"].ToString());
                        CancelledQty = int.Parse(dr["qty_cancelled"].ToString());
                        StopLossPrice = double.Parse(dr["stoploss_price"].ToString());
                        UpdatedAt = DateTime.Parse(dr["status_update_time"].ToString());
                        AlgoId = int.Parse(dr["algo_id"].ToString());

                        doesOrderRecordExist = true;
                    }
                }

            }
            catch (Exception ex)
            {
                // If we failed, trace the error for log analysis
                Logger.LogException(ex);
                throw;
            }

            return doesOrderRecordExist;
        }

        /// <summary>
        /// Persists the OrderRecord record in database
        /// </summary>
        private void InsertIntoDatabase()
        {
            // Must be a new orderRecord to insert, Throw an exception if existing orderRecord is tried to insert
            if (!_isNewOrderRecord)
                throw new System.InvalidOperationException("OrderRecord Id: " + OrderRefenceNumber +
                                                           "already exists in the System");


            var dbLib = new DBLibrary();

            dbLib.ExecuteProcedure("sp_insert_st_derivatives_orderbook", DbParamList(0));

            // Now it is no more a new orderRecord as it is entered into the system
            _isNewOrderRecord = false;

        }

        /// <summary>
        /// Persist the orderRecord information into DB
        /// </summary>
        public void Persist()
        {
            UpdateIntoDatabase();
            // Insert into DB if new orderRecord, update if orderRecord already exists in system
            //if (_isNewOrderRecord)
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

            paramList.Add(dbUtilities.CreateSqlParamater("@contractName", SqlDbType.NVarChar, 100, ParameterDirection.Input, this.ContractName));
            paramList.Add(dbUtilities.CreateSqlParamater("@orderDate", SqlDbType.DateTime, ParameterDirection.Input, this.OrderDate));
            paramList.Add(dbUtilities.CreateSqlParamater("@orderRef", SqlDbType.VarChar, 30, ParameterDirection.Input, this.OrderRefenceNumber));
            paramList.Add(dbUtilities.CreateSqlParamater("@direction", SqlDbType.SmallInt, ParameterDirection.Input, this.Direction));
            paramList.Add(dbUtilities.CreateSqlParamater("@qty", SqlDbType.Int, ParameterDirection.Input, this.Quantity));
            paramList.Add(dbUtilities.CreateSqlParamater("@price", SqlDbType.Decimal, ParameterDirection.Input, this.Price));
            paramList.Add(dbUtilities.CreateSqlParamater("@orderStatus", SqlDbType.SmallInt, ParameterDirection.Input, this.OrderStatus));
            paramList.Add(dbUtilities.CreateSqlParamater("@exchange", SqlDbType.SmallInt, ParameterDirection.Input, this.Exchange));
            paramList.Add(dbUtilities.CreateSqlParamater("@qtyOpen", SqlDbType.Int, ParameterDirection.Input, this.OpenQty));
            paramList.Add(dbUtilities.CreateSqlParamater("@qtyExecuted", SqlDbType.Int, ParameterDirection.Input, this.ExecutedQty));
            paramList.Add(dbUtilities.CreateSqlParamater("@qtyCancelled", SqlDbType.Int, ParameterDirection.Input, this.CancelledQty));
            paramList.Add(dbUtilities.CreateSqlParamater("@qtyExpired", SqlDbType.Int, ParameterDirection.Input, this.ExpiredQty));
            paramList.Add(dbUtilities.CreateSqlParamater("@stoplossPrice", SqlDbType.Decimal, ParameterDirection.Input, this.StopLossPrice));
            paramList.Add(dbUtilities.CreateSqlParamater("@algoId", SqlDbType.Int, ParameterDirection.Input, this.AlgoId)); 
            paramList.Add(dbUtilities.CreateSqlParamater("@statusUpdateAt", SqlDbType.VarChar, 127, ParameterDirection.Input, DateTime.Now));
            paramList.Add(dbUtilities.CreateSqlParamater("@upsertStatus", SqlDbType.Bit, ParameterDirection.Output));

            //switch (cmdType)
            //{
            //    case 0:
            //        paramList.Add(dbUtilities.CreateSqlParamater("@insertStatus", SqlDbType.Bit, ParameterDirection.Output));
            //        break;
            //    case 1:
            //        paramList.Add(dbUtilities.CreateSqlParamater("@updateStatus", SqlDbType.Bit, ParameterDirection.Output));
            //        break;
            //    default:
            //        break;
            //}

            return paramList;
        }


    }
}
