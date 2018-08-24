using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using StockTrader.Core;
using StockTrader.Platform.Logging;
using StockTrader.Utilities;

namespace SimpleTrader
{
    public class SimultaneousBuySell : SimultaneousBuySellBase
    {
        public SimultaneousBuySell(TradeParams tradeParams) : base(tradeParams)
        {
        }

        public override void StockBuySell()
        {
            Init(AlgoType.SimultaneousBuySell);

            while (MarketUtils.IsMarketOpen())
            {
                try
                {
                    var newTrades = new Dictionary<string, EquityTradeBookRecord>();

                    // refresh trade book
                    errCode = broker.GetEquityTradeBookToday(true, stockCode, out newTrades);

                    if (newTrades.Count > 0)
                    {
                        var trade = newTrades.First().Value;
                        Trace(string.Format(tradeTraceFormat, stockCode, trade.Direction == OrderDirection.BUY ? "bought" : "sold", trade.Quantity, trade.Price, newTrades.ContainsKey(holdingSellOrderRef) ? "CASH" : "MARGIN"));

                        if (newTrades.ContainsKey(todayOutstandingBuyOrderRef))
                        {
                            todayOutstandingQty += trade.Quantity;
                            todayOutstandingPrice = trade.Price;
                            todayOutstandingBuyOrderRef = "";
                        }

                        else if (newTrades.ContainsKey(todayOutstandingSellOrderRef))
                        {
                            todayOutstandingQty -= trade.Quantity;
                            todayOutstandingPrice = trade.Price;
                            todayOutstandingSellOrderRef = "";
                        }

                        else if (newTrades.ContainsKey(holdingSellOrderRef))
                        {
                            holdingOutstandingQty = 0;
                            holdingOutstandingPrice = 0;
                            holdingSellOrderRef = "";

                            UpdatePositionFile(holdingOutstandingQty, holdingOutstandingPrice, holdingSellOrderRef);
                        }

                        else
                        {
                            // broker squared off the sell. verify it and cancel the buy order

                            if (trade.Direction == OrderDirection.BUY)
                            {
                                CancelEquityOrder("Broker squared off the sell", todayOutstandingBuyOrderRef, trade.Direction);

                                todayOutstandingQty += trade.Quantity;
                                todayOutstandingPrice = trade.Price;
                                todayOutstandingBuyOrderRef = "";
                            }
                        }
                    }

                    // if order time is within range and today's buy order count is below max then only place pair
                    if (IsOrderTimeWithinRange() && string.IsNullOrEmpty(todayOutstandingBuyOrderRef) && string.IsNullOrEmpty(todayOutstandingSellOrderRef) && buyOrderCount < maxBuyOrders && (todayOutstandingQty + holdingOutstandingQty) < maxAllowedOutstandingQty)
                    {
                        // no pair existing, place a pair order

                        // place buy order, update buy order ref
                        var buyPrice = GetBuyPrice();
                        errCode = PlaceEquityOrder(stockCode, ordQty, buyPrice.ToString(), OrderPriceType.LIMIT, OrderDirection.BUY, EquityOrderType.MARGIN, Exchange.NSE, out todayOutstandingBuyOrderRef);

                        // place new sell order, update sell order ref
                        var sellPrice = GetSellPrice(buyPrice, false);
                        errCode = PlaceEquityOrder(stockCode, ordQty, sellPrice.ToString(), OrderPriceType.LIMIT, OrderDirection.SELL, EquityOrderType.MARGIN, Exchange.NSE, out todayOutstandingSellOrderRef);
                    }
                    else if (MarketUtils.IsTimeAfter3() && (!string.IsNullOrEmpty(todayOutstandingBuyOrderRef) && !string.IsNullOrEmpty(todayOutstandingSellOrderRef)))
                    {
                        // if existing pair is unexecuted, then cancel the pair
                        Trace("Cancelling the pair orders as no side is executed and its past 3 pm");

                        errCode = CancelEquityOrder("Unexecuted Pair cancellation", todayOutstandingBuyOrderRef, OrderDirection.BUY);
                        if (errCode == BrokerErrorCode.Success)
                            todayOutstandingBuyOrderRef = "";

                        errCode = CancelEquityOrder("Unexecuted Pair cancellation", todayOutstandingSellOrderRef, OrderDirection.SELL);
                        if (errCode == BrokerErrorCode.Success)
                            todayOutstandingSellOrderRef = "";
                    }

                    TrySquareOffNearEOD(AlgoType.SimultaneousBuySell);
                }
                catch (Exception ex)
                {
                    Trace("Error:" + ex.Message + "\nStacktrace:" + ex.StackTrace);
                }

                if (MarketUtils.IsTimeAfter325())
                    ConvertToDeliveryAndUpdatePositionFile();

                PauseBetweenTradeBookCheck();
            }

            ConvertToDeliveryAndUpdatePositionFile();
        }
    }
}
