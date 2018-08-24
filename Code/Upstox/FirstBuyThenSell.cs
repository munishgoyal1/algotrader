using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StockTrader.Core;
using StockTrader.Platform.Logging;
using StockTrader.Utilities;

namespace SimpleTrader
{
    public class FirstBuyThenSell: BuySellBase
    {
        public FirstBuyThenSell(TradeParams tradeParams) : base(tradeParams)
        {
        }

        public override void StockBuySell()
        {
            Init();

            // place buy order if eligible: if there is no pending buy order and if totaloutstanding qt is less than maxoutstanding
            if (string.IsNullOrEmpty(buyOrderRef) && todayOutstandingQty == 0 && (todayOutstandingQty + holdingOutstandingQty) < maxOutstandingQty)
            {
                // place buy order, update buy order ref
                var buyPrice = GetBuyPrice();
                errCode = PlaceMarginOrder(stockCode, ordQty, buyPrice.ToString(), OrderPriceType.LIMIT, OrderDirection.BUY, EquityOrderType.MARGIN, exchange, out buyOrderRef);
            }

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
                        Trace(string.Format(tradeTraceFormat, stockCode, trade.Direction == OrderDirection.BUY ? "bought" : "sold", trade.Quantity, trade.Price,
                            newTrades.ContainsKey(holdingSellOrderRef) ? "CASH" : "MARGIN"));

                        // if buy executed, then place a corresponding margin sell order
                        if (newTrades.ContainsKey(buyOrderRef))
                        {
                            // update outstanding qty and outstanding price to place updated sell order
                            todayOutstandingQty = trade.Quantity;
                            todayOutstandingPrice = trade.Price;

                            settlementNumber = trade.SettlementNumber;

                            // place new sell order, update sell order ref
                            var sellPrice = GetSellPrice(todayOutstandingPrice, false);
                            errCode = PlaceMarginOrder(stockCode, ordQty, sellPrice.ToString(), OrderPriceType.LIMIT, OrderDirection.SELL, EquityOrderType.MARGIN, exchange, out sellOrderRef);
                        }

                        // if sell executed, then place a fresh buy order
                        if (newTrades.ContainsKey(sellOrderRef))
                        {
                            todayOutstandingQty = 0;
                            todayOutstandingPrice = 0;

                            if (string.IsNullOrEmpty(buyOrderRef))
                            {
                                if (buyOrderCount >= maxBuyOrders)
                                    break;

                                // place buy order, update buy order ref
                                var buyPrice = GetBuyPrice();
                                errCode = PlaceMarginOrder(stockCode, ordQty, buyPrice.ToString(), OrderPriceType.LIMIT, OrderDirection.BUY, EquityOrderType.MARGIN, exchange, out buyOrderRef);
                            }
                        }

                        // if holding sell executed
                        if (newTrades.ContainsKey(holdingSellOrderRef))
                        {
                            holdingOutstandingQty = 0;
                            holdingOutstandingPrice = 0;
                            holdingSellOrderRef = "";

                            var positionStr = string.Format("{0} {1} {2}", todayOutstandingQty, todayOutstandingPrice, holdingSellOrderRef);
                            File.WriteAllText(positionFile, positionStr);

                            if (string.IsNullOrEmpty(buyOrderRef) && todayOutstandingQty == 0)
                            {
                                if (buyOrderCount >= maxBuyOrders)
                                    break;

                                // place buy order if eligible: if there is no pending buy order and if totaloutstanding qt is less than maxoutstanding
                                if ((todayOutstandingQty + holdingOutstandingQty) < maxOutstandingQty)
                                {
                                    // place buy order, update buy order ref
                                    var buyPrice = GetBuyPrice();
                                    errCode = PlaceMarginOrder(stockCode, ordQty, buyPrice.ToString(), OrderPriceType.LIMIT, OrderDirection.BUY, EquityOrderType.MARGIN, exchange, out buyOrderRef);
                                }
                            }
                        }
                    }

                    TrySquareOffNearEOD();
                }
                catch (Exception ex)
                {
                    Trace("Error:" + ex.Message + "\nStacktrace:" + ex.StackTrace);
                }

                Thread.Sleep(1000 * 90);
            }

            // convert to delivery any open buy position
            ConvertToDeliveryAndUpdatePositionFile();
        }

        public new double GetBuyPrice()
        {
            var calculatedBuyPrice = base.GetBuyPrice();

            return Math.Min(calculatedBuyPrice, buyPriceCap);
        }
    }
}
