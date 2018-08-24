using System.Collections.Generic;
using System.Threading;
using StockTrader.API.TradingAlgos;

namespace StockTrader.Core
{
    ////////////////////////////////////////////////////
    //////      ACCOUNT SUPPORTING CLASSES       ///////
    //////////////////////////////////////////////////// 

    // Stock, Algo to run and Broker Account binding
    public class BrokerSymbolAlgosObject
    {
        public BrokerSymbolAlgosObject(IBroker broker, Instrument instrument, List<ITradingAlgo> algos, object liveTickGenerator,
            int tickIntervalInMiliSecs = 10000)
        {
            Broker = broker;
            Algos = algos;
            Instrument = instrument;
            if (Instrument.InstrumentType == InstrumentType.Share)
                ETicks = (EquitySymbolLiveTickGenerator)liveTickGenerator;
            else
                DTicks = (DerivativeSymbolLiveTickGenerator)liveTickGenerator;

            DoStopThread = false;
            TickIntervalInMiliSeconds = tickIntervalInMiliSecs;
        }

        public IBroker Broker;
        public List<ITradingAlgo> Algos;
        public Instrument Instrument;
        public EquitySymbolLiveTickGenerator ETicks;
        public DerivativeSymbolLiveTickGenerator DTicks;
        public int TickIntervalInMiliSeconds;

        public bool DoStopThread;
    }

    // Per-stock thread object
    public struct SymbolAlgosThread
    {
        public Thread thread;
        public BrokerSymbolAlgosObject BrokerSymbolAlgosObj;
    }

    // Broker Account object passed into per-BrokerAccount login thread
    public class BrokingAccountObject
    {
        public BrokingAccountObject(IBroker account, object customData = null, object customData2 = null)
        {
            Broker = account;
            DoStopThread = false;
            CustomData = customData;
            CustomData2 = customData2;
        }

        public IBroker Broker;
        public object CustomData;
        public object CustomData2;
        public bool DoStopThread;
    }

    // Per-Account thread object
    public struct BrokingAccountThread
    {
        public Thread thread;
        public BrokingAccountObject brokerAccountObj;
    }

    // Per-stock thread object
    public class GenericLifeThread
    {
        public Thread thread;
        public bool bStopThread;
    }
}