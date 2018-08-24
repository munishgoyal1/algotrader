using System.Collections;
using System.Data;
using System.Text;
using System.Threading;
using StockTrader.Platform.Database;

namespace StockTrader.Platform.Logging
{
    /// <summary>
    /// Summary description for LogManager
    /// </summary>
    public static class LogManager
    {
        static DBLibrary dbLib = new DBLibrary();
        static DBUtilities dbUtilities = new DBUtilities();
        
        /// <summary>
        /// Method to write log entry into database
        /// </summary>
        /// <param name="logEntry">Log entry</param>
        public static void LogWriteCallback(object logEntry)
        {
            ArrayList paramList = ((Log)logEntry).DbParamList(0);
            
            dbLib.ExecuteProcedure("sp_insert_pdr_logs", paramList);

            //cleanup
            paramList.Clear();
            paramList = null;
            logEntry = null;
        }

        /// <summary>
        /// Method to log
        /// Need to be refined
        /// Message type can be added like whether it is a warning or processfinish
        /// Message Display flag can be added. Whether message should be displayed to the user or is it for internal debugging
        /// this function can have many other forms as well
        /// Perhpas right place for this function could be log class itself
        /// </summary>
        /// <param name="logEntry">Log entry</param>
        public static void Write(Log logEntry)
        {
            ThreadPool.QueueUserWorkItem(LogWriteCallback, logEntry);
        }

        /// <summary>
        /// Method to read recent log
        /// </summary>
        /// <param name="reportId">Id of the report</param>
        /// <returns>log string</returns>
        public static string ReadRecentLog(string reportId)
        {
            string latestLog = string.Empty;

            ArrayList paramList = new ArrayList();
            paramList.Add(dbUtilities.CreateSqlParamater("@id", SqlDbType.VarChar, ParameterDirection.Input, reportId));
            
            DataSet ds = dbLib.ExecuteProcedureDS("sp_select_pdr_logs", paramList);

            if (ds != null &&
                ds.Tables != null &&
                 ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
            {
                latestLog = (ds.Tables[0].Rows[0])["status_message"].ToString();
            }
            
            return latestLog;
        }

        /// <summary>
        /// Method to read failure log for a report
        /// </summary>
        /// <param name="reportId">Id of the report</param>
        /// <returns>log string</returns>
        public static string ReadFailureLog(string id, bool isReportLevel)
        {
            StringBuilder failureLog = new StringBuilder();
            ArrayList paramList = new ArrayList();

            paramList.Add(dbUtilities.CreateSqlParamater("@processId1", SqlDbType.Int, ParameterDirection.Input, 199));
            paramList.Add(dbUtilities.CreateSqlParamater("@processId2", SqlDbType.Int, ParameterDirection.Input, 99));
            paramList.Add(dbUtilities.CreateSqlParamater("@processId3", SqlDbType.Int, ParameterDirection.Input, 9));
            paramList.Add(dbUtilities.CreateSqlParamater("@topNLogs", SqlDbType.Int, ParameterDirection.Input, 3));
            paramList.Add(dbUtilities.CreateSqlParamater("@id", SqlDbType.VarChar, ParameterDirection.Input, id));

            DataSet ds = dbLib.ExecuteProcedureDS("sp_select_pdr_logs_failureOnes", paramList);

            if (ds != null &&
                ds.Tables != null &&
                 ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
            {
                foreach (DataRow dr in ds.Tables[0].Rows)
                {
                    string processId = dr["process_id"].ToString();

                    string logLevel = processId == "199" ? "Job Level" : processId == "99" ? "Report Level" : "Competitor level";
                    failureLog.AppendLine();
                    failureLog.AppendLine("Log type: " + logLevel);
                    failureLog.AppendLine();
                    failureLog.AppendLine(dr["status_message"].ToString());
                }
            }

            return failureLog.ToString();
        }
    }

}