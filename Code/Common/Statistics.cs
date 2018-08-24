
using System;
using System.Collections;
using System.Data;
using StockTrader.Platform.Database;

namespace StockTrader.Common
{
    public class EODTradeStats
    {
        public string contract_name;
		public DateTime trade_date;
        public double market_direction_percentage;
        public double roi_percentage;
		public double expected_profit;
        public double actual_profit;
        public double actual_roi_percentage;
        public double brokerage;
		public int num_trades;
        public int quantity;
		public int num_profit_trades;
		public int num_loss_trades;
		public double average_profit_pertrade;
		public double average_loss_pertrade;
		public short algo_id;
		public int inmarket_time_in_minutes;
		public int number_of_ticks;
		public DateTime status_update_time;

        public double min_price;
        public double max_price;

        public string r1;
        public string r2;

        DBLibrary _dbLib = new DBLibrary("");

        public EODTradeStats(string R1, string R2)
        {
            r1 = R1;
            r2 = R2;
        }

        public void Persist()
        {
            _dbLib.ExecuteProcedure("sp_upsert_st_trading_eod_statsbook", DbParamList(1));
        }

        public ArrayList DbParamList(int cmdType)
        {
            ArrayList paramList = new ArrayList();

            DBUtilities dbUtilities = new DBUtilities();

            paramList.Add(dbUtilities.CreateSqlParamater("@contract_name", SqlDbType.NVarChar, 100, ParameterDirection.Input, this.contract_name));
            paramList.Add(dbUtilities.CreateSqlParamater("@trade_date", SqlDbType.DateTime, ParameterDirection.Input, this.trade_date));
            paramList.Add(dbUtilities.CreateSqlParamater("@num_trades", SqlDbType.Int, ParameterDirection.Input, this.num_trades));
            paramList.Add(dbUtilities.CreateSqlParamater("@num_profit_trades", SqlDbType.Int, ParameterDirection.Input, this.num_profit_trades));
            paramList.Add(dbUtilities.CreateSqlParamater("@num_loss_trades", SqlDbType.Int, ParameterDirection.Input, this.num_loss_trades));

            paramList.Add(dbUtilities.CreateSqlParamater("@min_price", SqlDbType.Decimal, ParameterDirection.Input, this.min_price));
            paramList.Add(dbUtilities.CreateSqlParamater("@max_price", SqlDbType.Decimal, ParameterDirection.Input, this.max_price));
            paramList.Add(dbUtilities.CreateSqlParamater("@quantity", SqlDbType.Int, ParameterDirection.Input, this.quantity));

            paramList.Add(dbUtilities.CreateSqlParamater("@actual_profit", SqlDbType.Decimal, ParameterDirection.Input, this.actual_profit));
            paramList.Add(dbUtilities.CreateSqlParamater("@expected_profit", SqlDbType.Decimal, ParameterDirection.Input, this.expected_profit));
            paramList.Add(dbUtilities.CreateSqlParamater("@average_profit_pertrade", SqlDbType.Decimal, ParameterDirection.Input, this.average_profit_pertrade));
            paramList.Add(dbUtilities.CreateSqlParamater("@average_loss_pertrade", SqlDbType.Decimal, ParameterDirection.Input, this.average_loss_pertrade));
            paramList.Add(dbUtilities.CreateSqlParamater("@roi_percentage", SqlDbType.Decimal, ParameterDirection.Input, this.roi_percentage));
            paramList.Add(dbUtilities.CreateSqlParamater("@market_direction_percentage", SqlDbType.Decimal, ParameterDirection.Input, this.market_direction_percentage));
            paramList.Add(dbUtilities.CreateSqlParamater("@actual_roi_percentage", SqlDbType.Decimal, ParameterDirection.Input, this.actual_roi_percentage));
            paramList.Add(dbUtilities.CreateSqlParamater("@brokerage", SqlDbType.Decimal, ParameterDirection.Input, this.brokerage));

            var param = dbUtilities.CreateSqlParamater("@r1", SqlDbType.NVarChar, 100, ParameterDirection.Input, this.r1);
            param.IsNullable = true;
            param.Value = param.Value ?? DBNull.Value;
            paramList.Add(param);

            param = dbUtilities.CreateSqlParamater("@r2", SqlDbType.NVarChar, 100, ParameterDirection.Input, this.r2);
            param.IsNullable = true;
            param.Value = param.Value ?? DBNull.Value;
            paramList.Add(param);

            paramList.Add(dbUtilities.CreateSqlParamater("@algo_id", SqlDbType.Int, ParameterDirection.Input, this.algo_id));
            paramList.Add(dbUtilities.CreateSqlParamater("@inmarket_time_in_minutes", SqlDbType.Int, ParameterDirection.Input, this.inmarket_time_in_minutes));
            paramList.Add(dbUtilities.CreateSqlParamater("@number_of_ticks", SqlDbType.Int, ParameterDirection.Input, this.number_of_ticks));

            paramList.Add(dbUtilities.CreateSqlParamater("@status_update_time", SqlDbType.DateTime, ParameterDirection.Input, this.status_update_time));
            paramList.Add(dbUtilities.CreateSqlParamater("@upsertStatus", SqlDbType.Bit, ParameterDirection.Output));


            return paramList;
        }
    }

    public class EOPTradeStats
    {
        public string contract_name;
        public DateTime start_date;//
        public DateTime end_date;//
        public int num_days;//
        public double average_trade_price;
        public double market_direction_percentage;
        public double roi_percentage;
        public double expected_profit;
        public double actual_profit;
        public double actual_roi_percentage;
        public double brokerage;
        public int num_trades;
        public int num_profit_trades;
        public int num_loss_trades;
        public double average_profit_pertrade;
        public double average_loss_pertrade;
        public short algo_id;
        public int inmarket_time_in_minutes;
        public int number_of_ticks;
        public DateTime status_update_time;
        public int quantity;

        public double min_price;
        public double max_price;

        public string r1;
        public string r2;

        DBLibrary _dbLib = new DBLibrary("");
        //static DBLibrary DBLib = new DBLibrary("");

        public EOPTradeStats(string R1, string R2)
        {
            r1 = R1;
            r2 = R2;
        }

        public void Persist()
        {
            _dbLib.ExecuteProcedure("sp_upsert_st_trading_eop_statsbook", DbParamList(1));
        }

        public ArrayList DbParamList(int cmdType)
        {
            ArrayList paramList = new ArrayList();

            DBUtilities dbUtilities = new DBUtilities();

            paramList.Add(dbUtilities.CreateSqlParamater("@contract_name", SqlDbType.NVarChar, 100, ParameterDirection.Input, this.contract_name));
            paramList.Add(dbUtilities.CreateSqlParamater("@start_date", SqlDbType.DateTime, ParameterDirection.Input, this.start_date));
            paramList.Add(dbUtilities.CreateSqlParamater("@end_date", SqlDbType.DateTime, ParameterDirection.Input, this.end_date));
            paramList.Add(dbUtilities.CreateSqlParamater("@num_days", SqlDbType.Int, ParameterDirection.Input, this.num_days));
            paramList.Add(dbUtilities.CreateSqlParamater("@num_trades", SqlDbType.Int, ParameterDirection.Input, this.num_trades));
            paramList.Add(dbUtilities.CreateSqlParamater("@num_profit_trades", SqlDbType.Int, ParameterDirection.Input, this.num_profit_trades));
            paramList.Add(dbUtilities.CreateSqlParamater("@num_loss_trades", SqlDbType.Int, ParameterDirection.Input, this.num_loss_trades));

            paramList.Add(dbUtilities.CreateSqlParamater("@min_price", SqlDbType.Decimal, ParameterDirection.Input, this.min_price));
            paramList.Add(dbUtilities.CreateSqlParamater("@max_price", SqlDbType.Decimal, ParameterDirection.Input, this.max_price));
            paramList.Add(dbUtilities.CreateSqlParamater("@quantity", SqlDbType.Int, ParameterDirection.Input, this.quantity));
            paramList.Add(dbUtilities.CreateSqlParamater("@average_trade_price", SqlDbType.Decimal, ParameterDirection.Input, this.average_trade_price));
            paramList.Add(dbUtilities.CreateSqlParamater("@expected_profit", SqlDbType.Decimal, ParameterDirection.Input, this.expected_profit));
            paramList.Add(dbUtilities.CreateSqlParamater("@actual_profit", SqlDbType.Decimal, ParameterDirection.Input, this.actual_profit));
            paramList.Add(dbUtilities.CreateSqlParamater("@average_profit_pertrade", SqlDbType.Decimal, ParameterDirection.Input, this.average_profit_pertrade));
            paramList.Add(dbUtilities.CreateSqlParamater("@average_loss_pertrade", SqlDbType.Decimal, ParameterDirection.Input, this.average_loss_pertrade));
            paramList.Add(dbUtilities.CreateSqlParamater("@roi_percentage", SqlDbType.Decimal, ParameterDirection.Input, this.roi_percentage));
            paramList.Add(dbUtilities.CreateSqlParamater("@actual_roi_percentage", SqlDbType.Decimal, ParameterDirection.Input, this.actual_roi_percentage));
            paramList.Add(dbUtilities.CreateSqlParamater("@market_direction_percentage", SqlDbType.Decimal, ParameterDirection.Input, this.market_direction_percentage));
            paramList.Add(dbUtilities.CreateSqlParamater("@brokerage", SqlDbType.Decimal, ParameterDirection.Input, this.brokerage));

            var param = dbUtilities.CreateSqlParamater("@r1", SqlDbType.NVarChar, 100, ParameterDirection.Input, this.r1);
            param.IsNullable = true;
            param.Value = param.Value ?? DBNull.Value;
            paramList.Add(param);

            param = dbUtilities.CreateSqlParamater("@r2", SqlDbType.NVarChar, 100, ParameterDirection.Input, this.r2);
            param.IsNullable = true;
            param.Value = param.Value ?? DBNull.Value;
            paramList.Add(param);

            paramList.Add(dbUtilities.CreateSqlParamater("@algo_id", SqlDbType.Int, ParameterDirection.Input, this.algo_id));
            paramList.Add(dbUtilities.CreateSqlParamater("@inmarket_time_in_minutes", SqlDbType.Int, ParameterDirection.Input, this.inmarket_time_in_minutes));
            paramList.Add(dbUtilities.CreateSqlParamater("@number_of_ticks", SqlDbType.Int, ParameterDirection.Input, this.number_of_ticks));

            paramList.Add(dbUtilities.CreateSqlParamater("@status_update_time", SqlDbType.DateTime, ParameterDirection.Input, this.status_update_time));
            paramList.Add(dbUtilities.CreateSqlParamater("@upsertStatus", SqlDbType.Bit, ParameterDirection.Output));


            return paramList;
        }


        public static bool DoesExistEOPStatsForMinMaxAlgos(string contractName,
            short algoId,
            double marketDirection,
            string r1,
            string r2)
        {
            //EOPTradeStats eopTradeStats = null;
            DBUtilities dbUtilities = new DBUtilities();
            DBLibrary dbLib = new DBLibrary("");

            ArrayList paramList = new ArrayList
                                      {
                                          dbUtilities.CreateSqlParamater("@contract_name", SqlDbType.VarChar, 100,
                                                                         ParameterDirection.Input, contractName),
                                          dbUtilities.CreateSqlParamater("@market_direction_percentage",
                                                                         SqlDbType.Decimal, ParameterDirection.Input,
                                                                         marketDirection),
                                          dbUtilities.CreateSqlParamater("@algo_id", SqlDbType.SmallInt,
                                                                         ParameterDirection.Input, algoId)
                                      };

            var param = dbUtilities.CreateSqlParamater("@r1", SqlDbType.VarChar, 100, ParameterDirection.Input, r1);
            param.IsNullable = true;
            param.Value = param.Value ?? DBNull.Value;
            paramList.Add(param);

            param = dbUtilities.CreateSqlParamater("@r2", SqlDbType.VarChar, 100, ParameterDirection.Input, r2);
            param.IsNullable = true;
            param.Value = param.Value ?? DBNull.Value;
            paramList.Add(param);

            // Query the database
            DataSet ds = dbLib.ExecuteProcedureDS("sp_select_st_trading_eop_statsbook", paramList);

            if (ds != null &&
                ds.Tables != null &&
                ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
            {
                return true;
            }

            return false;
        }
    }
}