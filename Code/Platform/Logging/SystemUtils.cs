
//using StockTrader.Core;
//using StockTrader.Stocks.Utilities.Trader; 

using System;
using System.IO;
using System.Threading;
using System.Configuration;
namespace StockTrader.Platform.Logging
{

    public static class SystemUtils
    {
        static object syncRoot = new object();

        public static string GetStockFilesPath()
        {
            var path = ConfigurationManager.AppSettings.Get("StockFilesPath");
            if (string.IsNullOrEmpty(path))
                path = @"C:\StockRunFiles";

            return path;
        }

        public static string GetPositionFilesLocation()
        {
            string path = Path.Combine(GetStockFilesPath(), "PositionFiles");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }

        public static string GetPnLFilesLocation()
        {
            string path = Path.Combine(GetStockFilesPath(), "PnLFiles");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }

        public static string GetMessagesStoreLocation()
        {
            string path = Path.Combine(GetStockFilesPath(), "Messages\\" + DateTime.Today.Date.ToString("ddMMyyyy"));
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }

        public static string GetXmppMessagesFile(string desc)
        {
            string file = "Xmpp-" + desc + ".txt";
            string filePath = Path.Combine(GetMessagesStoreLocation(), file);
            return filePath;
        }

        public static string GetStockFilesBackupLocation()
        {
            string backupPath = Path.Combine(GetStockFilesPath(), "TraderStateFilesBackup\\" + DateTime.Today.Date.ToString("ddMMyyyy"));
            if (!Directory.Exists(backupPath))
                Directory.CreateDirectory(backupPath);

            return backupPath;
        }

        public static string GetAlgoStateLocation()
        {
            string path = Path.Combine(GetStockFilesPath(), "AlgoState\\" + DateTime.Today.Date.ToString("ddMMyyyy"));
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }

        public static string GetAlgoStateFileName(string description)
        {
            string file = "AlgoState" + "-" + description + ".txt";
            string filePath = Path.Combine(GetAlgoStateLocation(), file);
            return filePath;
        }

        public static string GetAlgoPositionsLocation()
        {
            string path = Path.Combine(GetStockFilesPath(), "AlgoState\\" + DateTime.Today.Date.ToString("ddMMyyyy"));
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }

        public static string GetAlgoPositionsFileName(string description)
        {
            string file = "AlgoPositions" + "-" + description + ".txt";
            string filePath = Path.Combine(GetAlgoPositionsLocation(), file);
            return filePath;
        }

        public static string GetStockLogsLocation()
        {
            string logPath = Path.Combine(GetStockFilesPath(), "TraderLogs\\" + DateTime.Today.Date.ToString("ddMMyyyy"));
            if (!Directory.Exists(logPath))
                Directory.CreateDirectory(logPath);
            return logPath;
        }

        public static string GetStockLogsFilePath()
        {
            string logFilePath = Path.Combine(GetStockLogsLocation(), "STLog-" + DateTime.Now.ToString("HHmmss") + ".txt");
            return logFilePath;
        }

        public static string GetStockTickFilesLocation()
        {
            string tickPath = Path.Combine(GetStockFilesPath(), "TickFiles\\" + DateTime.Today.Date.ToString("ddMMyyyy"));
            if (!Directory.Exists(tickPath))
                Directory.CreateDirectory(tickPath);
            return tickPath;
        }

        public static string GetTickFileName(string description)
        {
            string file = description + "-" + DateTime.Now.ToString("HHmmss");
            string ext = ".txt";
            string tickFilePath = Path.Combine(GetStockTickFilesLocation(), file + ext);
            if (File.Exists(tickFilePath))
                tickFilePath = Path.Combine(GetStockTickFilesLocation(), file + Guid.NewGuid() + ext);
            return tickFilePath;
        }

        public static string GetTraderErrorLogLocation()
        {
            string errorLogPath = Path.Combine(GetStockFilesPath(), "Errors\\" + DateTime.Today.Date.ToString("ddMMyyyy"));
            if (!Directory.Exists(errorLogPath))
                Directory.CreateDirectory(errorLogPath);
            return errorLogPath;
        }

        public static string GetErrorFileName(string description, bool isHtml = false)
        {
            lock (syncRoot)
            {
                string extn = isHtml ? ".html" : ".txt";
                Thread.Sleep(100);
                long rand = DateTime.Now.Ticks;// new Random().Next();
                string errorFilePath = Path.Combine(GetTraderErrorLogLocation(), description + "-" + rand + "-" + DateTime.Now.ToString("HHmm") + extn);
                return errorFilePath;
            }
        }


        public static string GetStockRunTempLocation()
        {
            string tempPath = Path.Combine(GetStockFilesPath(), "Temp\\" + DateTime.Today.Date.ToString("ddMMyyyy"));
            if (!Directory.Exists(tempPath))
                Directory.CreateDirectory(tempPath);
            return tempPath;
        }

        public static string GetStockRunTempRandomFileName(string description)
        {
            string tempFilePath = Path.Combine(GetStockRunTempLocation(), description + "-" + Guid.NewGuid().ToString() + "-" + DateTime.Now.ToString("HHmm") + ".txt");
            return tempFilePath;
        }

        public static string GetStockRunTempFileName(string description)
        {
            string tempFilePath = Path.Combine(GetStockRunTempLocation(), description + DateTime.Now.ToString("HHmmss") + ".txt");
            return tempFilePath;
        }
    }
}
