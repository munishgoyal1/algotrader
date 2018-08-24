using System;
using System.Collections.Generic;
using StockTrader.Core;
using StockTrader.API.TradingAlgos;
using System.Text;
using StockTrader.Platform.Logging;
using System.Linq;

namespace StockTrader.Utilities.Broker
{
    public class StockUtils
    {
        public static DTick GetNearestOptionTick(DTick futTick, Dictionary<int, Dictionary<int, SymbolTick>> tickStore, ref int strkPrc, DTick optTick, bool determineOwnStrkPrc = false, bool isBuy = false)
        {
            if (tickStore.Keys.Count == 0) return null;

            if (determineOwnStrkPrc)
            {
                bool isCall = IsCallOption(tickStore.First().Value.First().Value.I.InstrumentType);
                strkPrc = StockUtils.GetNearestOptionStrikePrice(futTick.Di, isCall, isBuy);

                var tries = 0;
                while (!tickStore.ContainsKey(strkPrc) && tries++ < 4)
                {
                    if (tickStore.Last().Key < strkPrc)
                        strkPrc -= 100;
                    else if(tickStore.First().Key > strkPrc)
                        strkPrc += 100;
                }
                if (!tickStore.ContainsKey(strkPrc))
                    strkPrc = tickStore.First().Key;
            }

            DateTime datetime = futTick.Di.QuoteTime;

            if (!tickStore.ContainsKey(strkPrc))
                return null;

            int time = datetime.Hour * 100 + datetime.Minute;

            var prcStore = tickStore[strkPrc];
            SymbolTick si = null;
            int maxTimeWindow = 15;
            do
            {
                if (!prcStore.ContainsKey(time))
                {
                    if (time % 100 >= 60)
                        time = time - 60 + 100;
                    time++;
                }
                else
                    si = prcStore[time];

                maxTimeWindow--;
            } while (si == null && maxTimeWindow > 0);

            if (si == null && optTick.Di == null)
            {
                if (prcStore.Count > 0)
                    si = prcStore.First().Value;
                else
                    optTick.Di = new DerivativeSymbolQuote(1, 1, 1);
            }

            return si == null ? optTick : new DTick(si.D.Q, 0);
        }

        public static int GetNearestOptionStrikePrice(DerivativeSymbolQuote di, bool isCall, bool isBuy)
        {
            var symbol = di.UnderlyingSymbol;
            //var isCall = StockUtils.IsCallOption(di.InstrumentType);
            if (symbol != "NIFTY" && symbol != "CNXBAN")
                return -1;

            var price = (int)di.LastTradedPriceDouble;

            var mod = price % 100;

            if (isCall)
            {
                price = price - mod + 100;
                if (isBuy)
                    price += 100;
                return price;
            }
            else
            {
                price = price - mod;
                if (!isBuy)
                    price -= 100;
                return price - mod;
            }
        }

        public static double GetPriceForAnalysis(DerivativeSymbolQuote di)
        {
            //if (AlgoParams.UseProbableTradeValue)
            //{
            //    if (di.ProbableNextTradeValue != -1)
            //        return di.ProbableNextTradeValue;
            //}
            if (di.LastTradedPriceDouble > 0)
            {
                return di.LastTradedPriceDouble;
            }
            return di.LastTradedPriceDouble;
        }

        public static string CollateAlgoStates(ICollection<TradingAlgo> algos)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                foreach (var algo in algos)
                {
                    var openPosDesc = "No Open.";
                    // Alert on the current posiitons state
                    if (algo.S.TotalBuyTrades != algo.S.TotalSellTrades)
                    {
                        int profitAmt = (int)algo.GetSquareOffProfitAmt();
                        var openPos = algo.GetOpenPosition();
                        var pnl = profitAmt > 0 ? "PROFIT" : "LOSS";
                        openPosDesc = string.Format("Open={0} {1}, {2} at {3}.",
                            pnl,
                            profitAmt,
                            openPos.OrderPosition,
                            openPos.OrderPrice);
                    }

                    openPosDesc = string.Format(openPosDesc + "LTP: {0}", algo.S.Ticks.CurrTick.Di.LastTradedPriceDouble);

                    var desc = string.Format("{0} {1}: T({2})={3}." + openPosDesc,
                        algo.AlgoParams.I.Symbol,
                        algo.AlgoWorkingState,
                        Math.Min(algo.S.TotalBuyTrades, algo.S.TotalSellTrades),
                        (int)algo.S.TotalActualNettProfitAmt);
                    sb.AppendLine(desc);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                sb.AppendLine("Error: " + ex.Message);
            }
            return sb.ToString();
        }

        public static bool IsAnyOpenPosition(ICollection<TradingAlgo> algos)
        {
            bool isAnyOpenPos = false;
            try
            {
                foreach (var algo in algos)
                {
                    if (algo.S.TotalBuyTrades != algo.S.TotalSellTrades)
                    {
                        isAnyOpenPos = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
            return isAnyOpenPos;
        }

        // Instrument description strings
        public static string GetInstrumentTypeString(InstrumentType instrumentType)
        {
            string instrumentCode = string.Empty;
            if (instrumentType != InstrumentType.Share)
            {
                // default is option
                instrumentCode = "OPT";

                if (instrumentType == InstrumentType.FutureIndex || instrumentType == InstrumentType.FutureStock)
                {
                    instrumentCode = "FUT";
                }
            }
            return instrumentCode;
        }


        public static string GetFnOInstrumentDirectionString(InstrumentType instrumentType)
        {
            string instrumentDirectionCode = string.Empty;

            if (instrumentType == InstrumentType.OptionCallIndex || instrumentType == InstrumentType.OptionCallStock)
                instrumentDirectionCode = "CE";
            else if (instrumentType == InstrumentType.OptionPutIndex || instrumentType == InstrumentType.OptionPutStock)
                instrumentDirectionCode = "PE";

            return instrumentDirectionCode;
        }

        public static bool IsCallOption(InstrumentType instrumentType)
        {
            if (instrumentType == InstrumentType.FutureIndex ||
                instrumentType == InstrumentType.FutureStock || instrumentType == InstrumentType.Share)
                return false;

            if (instrumentType == InstrumentType.OptionCallIndex || instrumentType == InstrumentType.OptionCallStock)
                return true;

            return false;
        }

        public static string GetInstrumentTypeForGetQuoteString(InstrumentType instrumentType)
        {
            string instrumentCode = string.Empty;
            if (instrumentType != InstrumentType.Share)
            {
                // default is option
                instrumentCode = "O";

                if (instrumentType == InstrumentType.FutureIndex || instrumentType == InstrumentType.FutureStock)
                {
                    instrumentCode = "F";
                }
            }
            return instrumentCode;
        }

        public static string GetFnOInstrumentGetQuoteDirectionString(InstrumentType instrumentType)
        {
            string instrumentDirectionCode = string.Empty;

            if (instrumentType == InstrumentType.OptionCallIndex || instrumentType == InstrumentType.OptionCallStock)
            {
                instrumentDirectionCode = "C";
            }
            else if (instrumentType == InstrumentType.OptionPutIndex || instrumentType == InstrumentType.OptionPutStock)
            {
                instrumentDirectionCode = "P";
            }

            return instrumentDirectionCode;
        }


        public static string GetInstrumentDescriptionString(InstrumentType instrumentType,
                                                            string symbol,
                                                            DateTime expiryDate,
                                                            double strikePrice)
        {
            string instrumentDescription = string.Empty;

            if (instrumentType == InstrumentType.Share)
            {
                instrumentDescription += "SHARE";
                return instrumentDescription;
            }

            string instrumentCode = GetInstrumentTypeString(instrumentType);
            string expiryDateString = expiryDate.ToString("dd-MMM-yyyy");
            instrumentDescription += instrumentCode + "-" + symbol + "-" + expiryDateString;

            if (IsInstrumentOptionType(instrumentType))
            {
                string instrumentDirection = GetFnOInstrumentDirectionString(instrumentType);
                instrumentDescription += "-" + strikePrice.ToString() + "-" + instrumentDirection;
            }
            return instrumentDescription;
        }

        public static bool IsInstrumentOptionType(InstrumentType instrumentType)
        {
            return (instrumentType == InstrumentType.OptionCallIndex || instrumentType == InstrumentType.OptionCallStock ||
                instrumentType == InstrumentType.OptionPutIndex || instrumentType == InstrumentType.OptionPutStock);
        }

        // Orderbook and Tradebook util methods

        public static void PersistOrderbookRecords(List<DerivativeOrderBookRecord> dois)
        {
            foreach (DerivativeOrderBookRecord doi in dois)
            {
                DerivativeOrderBookRecord dor = new DerivativeOrderBookRecord(doi);
                dor.Persist();
            }
        }

        public static void PersistTradebookRecords(List<DerivativeTradeBookRecord> dtis)
        {
            foreach (DerivativeTradeBookRecord dti in dtis)
            {
                DerivativeTradeBookRecord dtr = new DerivativeTradeBookRecord(dti);
                dtr.Persist();
            }
        }

        // Clone a OrderBook dictionary
        public static Dictionary<string, EquityOrderBookRecord> CloneDictEquityOrder(Dictionary<string, EquityOrderBookRecord> oldDict)
        {
            // TODO: clear up and use just this one statement
            return (new Dictionary<string, EquityOrderBookRecord>(oldDict));
            //Dictionary<string, EquityOrderBookRecord> newDict = new Dictionary<string, EquityOrderBookRecord>();
            //foreach (KeyValuePair<string, EquityOrderBookRecord> pair in oldDict)
            //    newDict[pair.Key] = pair.Value;
            //return newDict;
        }

        // Clone a TradeBook dictionary
        public static Dictionary<string, EquityTradeBookRecord> CloneDictEquityTrade(Dictionary<string, EquityTradeBookRecord> oldDict)
        {
            Dictionary<string, EquityTradeBookRecord> newDict = new Dictionary<string, EquityTradeBookRecord>();
            foreach (KeyValuePair<string, EquityTradeBookRecord> pair in oldDict)
                newDict[pair.Key] = pair.Value;
            return newDict;
        }

        public static int GetNearestStrikePrice(double price, int stepSize)
        {
            int strikePrice = (int)price;
            int diff = strikePrice % stepSize;
            if (stepSize > 2 * diff)
                strikePrice -= diff;
            else
                strikePrice += stepSize - diff;

            return strikePrice;
        }
    }
}
