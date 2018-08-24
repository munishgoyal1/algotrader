using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace StockTrader.Platform.Logging
{
    public enum TraceType
    {
        Error,
        Warning,
        Info,
        Noise,
        Unknown
    }

    public static class FileTracing
    {
        public static string mFilePath;
        public static string mLogFile;
        public static string mErrorFile;

        public static string mLogFileName;

        static object tracerLockObject = new object();
        static StreamWriter sw;
        static Action<string> _logBoxDel;

        static int fileNum = 1;
        static int checks = 0;
        
        static FileTracing()
        {
            mFilePath = SystemUtils.GetStockLogsLocation();
            mLogFile = SystemUtils.GetStockLogsFilePath();
            mLogFileName = Path.GetFileName(mLogFile);
            mLogFileName = mLogFileName.Remove(mLogFileName.IndexOf('.'));
            mErrorFile = Path.Combine(mFilePath, mLogFileName + "-Warnings-Errors.txt");

            Logger.Initialize(mErrorFile, "StockTrader", tracerLockObject);
            sw = new StreamWriter(mLogFile);
            sw.AutoFlush = true;
        }

        public static void SetTraceFilename(string filePath, string traceFileName)
        {
            lock (tracerLockObject)
            {
                if (sw != null) sw.Dispose();
                mFilePath = filePath;
                mLogFileName = traceFileName;
                mLogFile = Path.Combine(mFilePath, mLogFileName + ".txt");
                mLogFileName = Path.GetFileName(mLogFile);
                mLogFileName = mLogFileName.Remove(mLogFileName.IndexOf('.'));
                mErrorFile = Path.Combine(mFilePath, mLogFileName + "-Warnings-Errors.txt");

                Logger.ReInitialize(mErrorFile, "StockTrader", tracerLockObject);
                sw = new StreamWriter(mLogFile);
                sw.AutoFlush = true;
            }
        }

        public static string GetTraceFilename()
        {
            return mLogFile;
        }

        public static void SetTraceDelegate(Action<string> del)
        {
            _logBoxDel = del;
        }

        //public static void TraceVerbose(this TraceSource traceSource, string data)
        //{
        //    traceSource.TraceData(TraceEventType.Verbose, 0, data);
        //}

        public static void TraceOut(bool bTraceIfTrue, string traceString, TraceType traceType)
        {
            if (bTraceIfTrue)
            {
                TraceOut(traceString, traceType);
            }
        }

        public static void TraceOut(bool bTraceIfTrue, string traceString)
        {
            if (bTraceIfTrue)
            {
                TraceOut(traceString);
            }
        }

        public static void TraceOut(string traceString, TraceType traceType)
        {
            TraceOut(traceString);
        }

        public static void TraceOut(string traceString)
        {
            // Reset the tracefile if the size exceeds certain limit
            if ((checks++ % 1000) == 0)
            {
                FileInfo fi = new FileInfo(mLogFile);

                if (fi.Length > 1024 * 1024 * 10)
                {
                    fileNum++;
                    SetTraceFilename(mFilePath, mLogFileName.Remove(mLogFileName.LastIndexOf('-')) + "-" + fileNum);
                }
            }
            //return;
            lock (tracerLockObject)
            {
                //using (FileStream fs = new FileStream(mFileName, FileMode.Append, FileAccess.Write))
                {
                    //using (StreamWriter sw = new StreamWriter(fs))
                    {
                        //string timeAndThread = DateTime.Now.ToString("yyyyMMdd:HHmmss") + "::" + Thread.CurrentThread.Name;
                        string timeAndThread = DateTime.Now.ToString() + "::" + Thread.CurrentThread.Name;
                        traceString = timeAndThread + ": " + traceString + "\n";
                        sw.WriteLine(traceString);
                        //Console.WriteLine(traceString);
                        if (_logBoxDel != null)
                            _logBoxDel(traceString);
                    }
                }
            }
        }


        public static TraceSource[] mTraceSources;
        public static void InitializeTraceSources(string mTraceFileName)
        {
            mTraceSources = new TraceSource[2];

            TextWriterTraceListener twtl = new TextWriterTraceListener(mTraceFileName);

            mTraceSources[0] = new TraceSource("V:", SourceLevels.Verbose);
            mTraceSources[0].Listeners.Add(twtl);

            mTraceSources[1] = new TraceSource("I:", SourceLevels.Information);
            //mTraceSources[1].Listeners.Add(new System.Diagnostics.ConsoleTraceListener());
            mTraceSources[1].Listeners.Add(twtl);
        }

        public static void TraceInformation(string data)
        {
            TraceOut(data);
            //mTraceSources[0].TraceInformation(data);
            //mTraceSources[1].TraceInformation(data);
        }

        public static void TraceTradeInformation(string data)
        {
            TraceOut(data);
            //FileTracing.TraceOut(data);
            //mTraceSources[0].TraceInformation(data);
            //mTraceSources[1].TraceInformation(data);
        }

        public static void TraceImpAlgoInformation(string data)
        {
            TraceOut(data);
            //FileTracing.TraceOut(data);
            //mTraceSources[0].TraceInformation(data);
            //mTraceSources[1].TraceInformation(data);
        }

        public static void TraceVerbose(string data)
        {
            //mTraceSources[0].TraceVerbose(data);
        }

        public static void FlushTraces()
        {
            foreach (TraceSource ts in mTraceSources)
            {
                ts.Flush();
            }
        }
    }
}
