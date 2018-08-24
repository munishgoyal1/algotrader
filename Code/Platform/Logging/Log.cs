using System;
using System.Collections;
using System.Data;
using StockTrader.Platform.Database;

namespace StockTrader.Platform.Logging
{
    /// <summary>
    /// Summary description for Log
    /// </summary>
    public class Log
    {
        private string _id;
        private int _processId;
        private string _statusMessage;

        /// <summary>
        /// Constructor
        /// </summary>
        public Log()
        {
            _id = String.Empty;
            _processId = 0;
            _statusMessage = String.Empty;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="reportId"></param>
        public Log(string reportId)
        {
            _id = reportId;
            _processId = 0;
            _statusMessage = String.Empty;
        }

        /// <summary>
        /// Id of identifier like reportid
        /// </summary>
        public string Id
        {
            set { this._id = value; }
            get { return this._id; }
        }

        /// <summary>
        /// Id of the process that triggered log
        /// </summary>
        public int ProcessId
        {
            set { this._processId = value; }
            get { return this._processId; }
        }

        /// <summary>
        /// Log Message
        /// </summary>
        public string StatusMessage
        {
            set { this._statusMessage = value; }
            get { return this._statusMessage; }
        }

        /// <summary>
        /// Method to reset log properties
        /// </summary>
        public void Reset()
        {
            this._processId = 0;
            this._statusMessage = String.Empty;
        }

        /// <summary>
        /// Method to get database parameters
        /// </summary>
        /// <param name="cmdType">command type</param>
        /// <returns>parameters list</returns>
        public ArrayList DbParamList(int cmdType)
        {
            ArrayList paramList = new ArrayList();

            DBUtilities dbUtilities = new DBUtilities();

            paramList.Add(dbUtilities.CreateSqlParamater("@id", SqlDbType.VarChar, ParameterDirection.Input, this.Id));
            paramList.Add(dbUtilities.CreateSqlParamater("@process_id", SqlDbType.Int, ParameterDirection.Input, this.ProcessId));
            paramList.Add(dbUtilities.CreateSqlParamater("@status_message", SqlDbType.VarChar, ParameterDirection.Input, this.StatusMessage));

            return paramList;
        }
    }
}