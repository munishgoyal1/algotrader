using StockTrader.Brokers.UpstoxBroker;
using StockTrader.Core;
using StockTrader.Platform.Logging;
using StockTrader.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UpstoxNet;

namespace UpstoxTrader
{   
    public class MarketTrend
    {
        public BrokerErrorCode errCode;
        public string stockCode = null;
        public MyUpstoxWrapper myUpstoxWrapper = null;
        public Exchange exchange;
        public string exchStr;
        public double mktTrendFactorForBuyMarkdown = 1;
        public QuotesReceivedEventArgs quote;

        public MarketTrend(UpstoxMarketTrendParams @params)
        {
            myUpstoxWrapper = @params.upstox;
            stockCode = @params.stockCode;
            exchStr = @params.exchangeStr;
        }

        public void StartCapturingMarketTrend()
        {
            try
            {
                EquitySymbolQuote quote;
                errCode = myUpstoxWrapper.GetSnapQuote(exchStr, stockCode, out quote);

                myUpstoxWrapper.Upstox.QuotesReceivedEvent += new Upstox.QuotesReceivedEventEventHandler(QuoteReceived);
                var substatus = myUpstoxWrapper.Upstox.SubscribeQuotes(exchStr, stockCode);
                Trace(string.Format("SubscribeQuotes status={0}", substatus));
            }
            catch (Exception ex)
            {
                Trace(string.Format("{0} Error: {1} \nStacktrace:{2}", stockCode, ex.Message, ex.StackTrace));
                throw;
            }

            while (MarketUtils.IsMarketOpen())
            {
                try
                {
                    if(quote != null)
                    {
                        var changePct = (quote.LTP - quote.Close) / quote.Close;

                        var prevmktTrendFactorForBuyMarkdown = mktTrendFactorForBuyMarkdown;

                        if (changePct < -0.005)
                            mktTrendFactorForBuyMarkdown = 1.2;
                        else if (changePct < -0.01)
                            mktTrendFactorForBuyMarkdown = 1.5;
                        else if (changePct < -0.015)
                            mktTrendFactorForBuyMarkdown = 2;
                        else mktTrendFactorForBuyMarkdown = 1;

                        if (prevmktTrendFactorForBuyMarkdown != mktTrendFactorForBuyMarkdown)
                        {
                            Trace(string.Format("MarketTrendForBuyMarkdown changed from {0} to {1}. Nifty changePct={2}", prevmktTrendFactorForBuyMarkdown, mktTrendFactorForBuyMarkdown, Math.Round(changePct, 5)));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace("Error:" + ex.Message + "\nStacktrace:" + ex.StackTrace);
                }

                Thread.Sleep(1000 * 15);
            }
        }

        public void QuoteReceived(object sender, QuotesReceivedEventArgs args)
        {
            if (stockCode == args.TrdSym)
                Interlocked.Exchange(ref quote, args);
        }

        public void Trace(string message)
        {
            message = GetType().Name + " " + stockCode + " " + message;
            Console.WriteLine(DateTime.Now.ToString() + " " + message);
            FileTracing.TraceOut(message);
        }
    }
}
