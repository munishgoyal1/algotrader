using System;
using System.Collections;
using System.Diagnostics;
using System.Net.Mail;
using System.Text;
using System.IO;

namespace StockTrader.Platform.Logging
{

    public enum AppModuleIdentifier
    {
        StockTrader = 0, // Codes <= 100 are StockTrader for now)

        UI = 1, // > 100 and <= 200
    }

    public enum AppErrorType
    {
        DataInconsistency,

        EngineError,

        UIError,

        UncategorizedError
    }

    public static class Logger
    {
        static Logger()
        {
            
        }

        static bool bInited = false;
        static string _source = "Kalapudina: StockTrader";
        static string _eventBucket = "Application";
        static object _traceSyncObj = new object();
        static string _user = "";
        static string _password = "";
        static string _to = "";//, munish.goyal@credit-suisse.com";
        static SmtpClient GmailSender = new SmtpClient("smtp.gmail.com", 587);

        public static void SendEmail(string subject, string body)
        {
            MailMessage msg = new MailMessage(_user, _to);
            msg.Body = body;
            msg.Subject = subject;
            GmailSender.Send(msg);
        }

        public static void Initialize(string logsPath, string source, object traceSyncObj)
        {
            if (!bInited)
            {
                if (traceSyncObj != null)
                    _traceSyncObj = traceSyncObj;

                if (!string.IsNullOrEmpty(source))
                {
                    _source = source;
                }
                StartTracingListener(logsPath);
                bInited = true;
            }
        }

        public static void ReInitialize(string logsPath, string source, object traceSyncObj)
        {
            if (bInited)
            {
                if (traceSyncObj != null)
                    _traceSyncObj = traceSyncObj;

                if (!string.IsNullOrEmpty(source))
                {
                    _source = source;
                }
                Trace.Listeners.Clear();
                StartTracingListener(logsPath);
            }
        }

        public static void LogErrorText(string desc, string content)
        {
            try
            {
                File.WriteAllText(SystemUtils.GetErrorFileName(desc, true), content);
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
        }

        public static void LogAnEvent(string msg, EventLogEntryType logType = EventLogEntryType.Information)
        {
            if (!EventLog.SourceExists(_source))
            {
                EventLog.CreateEventSource(_source, _eventBucket);
            }

            EventLog eLog = new EventLog {Source = _source};

            eLog.WriteEntry(msg, logType);
        }

        public static void LogAnExceptionEvent(string msg)
        {
            LogAnEvent(msg, EventLogEntryType.Error);
        }

        public static void LogAnExceptionEvent(Exception ex)
        {
            var msg = BuildExceptionMessage(ex, false);

            LogAnEvent(msg, EventLogEntryType.Error);
        }

        private static void StartTracingListener(string logsPath)
        {
            //DirectoryInfo logDirectory = new DirectoryInfo(logsPath);

            //if (!logDirectory.Exists)
            //{
            //    logDirectory.Create();
            //}

            //string logFile = logDirectory.FullName + "\\" + Guid.NewGuid().ToString() + ".txt";

            // For current logging infra. logsPath is expected to be full file path
            string logFile = logsPath;

            var logListener = new TextWriterTraceListener(logFile);
            Trace.Listeners.Add(logListener);
            Trace.AutoFlush = true; // for performance reasons, dont make it true
        }

        public static void FlushTraces()
        {
            foreach (TraceListener ts in Trace.Listeners)
            {
                ts.Flush();
            }
        }

        public enum LogSeverity
        {
            Informational,

            Warning,

            Error
        }

        public static string GetExceptionData(Exception ex)
        {
            string exData = string.Empty; 
            string exDataMessage = string.Empty;

            foreach (DictionaryEntry pair in ex.Data)
            {
                exData += string.Format("{0} = {1}\n", pair.Key, pair.Value);
            }

            if (!string.IsNullOrEmpty(exData))
            {
                exDataMessage = "Exception Data: " + exData;
            }

            return exDataMessage;
        }

        public static void LogIt(string msg, LogSeverity severity = LogSeverity.Informational, bool logIfTrue = true)
        {
            // Dont log if logIfTrue is false
            if (!logIfTrue)
                return;

            lock (_traceSyncObj)
            {
                var logMessage = DateTime.Now + " : " + msg;
                FileTracing.TraceOut(logMessage);

                switch (severity)
                {
                    case LogSeverity.Informational:
                        Trace.TraceInformation(logMessage);
                        break;

                    case LogSeverity.Warning:
                        Trace.TraceWarning(logMessage);
                        break;

                    case LogSeverity.Error:
                        Trace.TraceError(logMessage);
                        break;

                }
            }
            //Trace.Flush();
        }

        //public static void LogToDB(string customMsg, string jobId, int errorCode)
        //{
        //    LogIt(customMsg);

        //    Log log = new Log();

        //    log.Id = jobId;
        //    log.StatusMessage = customMsg;
        //    log.ProcessId = errorCode;

        //    LogManager.LogWriteCallback(log);
        //}

        public static void LogMessageAndSendMail(string customMsg, string user = null, AppErrorType errorType = AppErrorType.UncategorizedError)
        {
            LogIt(customMsg);

            // Send email alert to engineering team
            //Emails.SendErrorInternalMail(user, customMsg, errorType);
        }

        public static void LogErrorMessage(string msg, bool logIfTrue = true)
        {
            LogIt(msg, LogSeverity.Error, logIfTrue);
        }

        public static void LogInformationalMessage(string msg, bool logIfTrue = true)
        {
            LogIt(msg, LogSeverity.Informational, logIfTrue);
        }

        public static void LogWarningMessage(string msg, bool logIfTrue = true)
        {
            LogIt(msg, LogSeverity.Warning, logIfTrue);
        }

        public static void LogException(string msgFormat, object[] msgParams)
        {
            LogIt(string.Format(msgFormat, msgParams), LogSeverity.Error);
        }

        public static void LogExceptionAndSendMail(Exception ex, string user, string customMsg = null, AppErrorType errorType = AppErrorType.UncategorizedError)
        {
            string logMessage = BuildExceptionMessage(ex, true, customMsg);
            LogErrorMessage(logMessage);

            // Send email alert to engineering team
            //Emails.SendErrorInternalMail(user, logMessage);
        }

        private static string BuildExceptionMessage(Exception ex, bool bIncludeStackTrace = true, string customMsg = null)
        {
            StringBuilder sb = new StringBuilder();

            if (!string.IsNullOrEmpty(customMsg))
            {
                sb.AppendLine("Custom MessagH: " + customMsg);
            }

            sb.AppendLine(BuildExceptionMessage(ex, bIncludeStackTrace));

            return sb.ToString();
        }

        public static void LogException(Exception ex, string customMsg = null, bool bIncludeStackTrace = true)
        {
            string logMessage = BuildExceptionMessage(ex, bIncludeStackTrace, customMsg);

            LogIt(logMessage, LogSeverity.Error);
        }

        public static void LogShortException(Exception ex, string customMsg = null)
        {
            LogException(ex, customMsg, false);
        }

        private static string BuildExceptionMessage(Exception ex, bool bIncludeStackTrace = true)
        {
            StringBuilder sb = new StringBuilder("Exception");
            sb.AppendLine("Reason: " + ex.Message);
            
            if (bIncludeStackTrace)
            {
                sb.AppendLine("StackTrace: " + ex.StackTrace);
            }
            
            return sb.ToString();
        }
    }

}
