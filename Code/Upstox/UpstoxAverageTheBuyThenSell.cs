using StockTrader.Core;
using StockTrader.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UpstoxTrader
{
    // assumptions:
    // 1. single buy or sell outsanding order at any time
    // 2. whenever a buy gets executed, latest holding avg is computed and sell order is updated accordingly
    // 3. todaysellorder and holdingsellorder are seperate
    // every time only new trades are checked and processed
    // if there is error in processing any trade then that trade will not be processed until the program is restarted
    public class UpstoxAverageTheBuyThenSell : UpstoxBuySellBase
    {
        public UpstoxAverageTheBuyThenSell(UpstoxTradeParams tradeParams) : base(tradeParams)
        {
            //ConvertPendingMarginPositionsToDelivery(stockCode, 4, 1, "2017084", OrderDirection.BUY, exchange);

        }

        private void PlaceBuyOrderIfEligible()
        {
            // start and end timings. If we already converted then dont place fresh buy orders
            if (!IsOrderTimeWithinRange() || isOutstandingPositionConverted)
                return;

            // place buy order if eligible: if there is no pending buy order and if totaloutstanding qty is less than maxoutstanding
            if (string.IsNullOrEmpty(todayOutstandingBuyOrderId) && todayBuyOrderCount < maxBuyOrdersAllowedInADay
                && (todayOutstandingQty + holdingOutstandingQty) < maxTotalOutstandingQtyAllowed
                && todayOutstandingQty < maxTodayOutstandingQtyAllowed)
            {
                double ltp;
                var errCode = GetLTP(out ltp);
                var calculatedOrderQty = ordQty;

                double lastPriceToCompareWith = ltp;
                string priceStrategy = "LTP Markdown";
                string qtyStrategy = "Default TodayOutstandingMultiple";

                // if outstanding, then use lastbuy
                // if 0 outstanding, use ltp
                // if 0 Ltp, then use holdingprice

                if (todayOutstandingQty > 0)
                {
                    lastPriceToCompareWith = lastBuyPrice;
                    priceStrategy = "LastBuyPrice Markdown";
                }
                else if (ltp > 0)
                {
                    lastPriceToCompareWith = ltp;
                    priceStrategy = "LTP Markdown";

                }
                else if (holdingOutstandingQty > 0)
                {
                    lastPriceToCompareWith = holdingOutstandingPrice;
                    priceStrategy = "HoldingPrice Markdown";
                }
                else
                {
                    Trace(string.Format("LTP:{0}, LastBuyPrice:{1}, HoldingOutstandingPrice:{2}. No reference price to place BUY order", ltp, lastBuyPrice, holdingOutstandingPrice));
                    return;
                }

                var totalOutstandingQty = todayOutstandingQty + holdingOutstandingQty;
                var todayOutstandingMultiple = ((todayOutstandingQty + ordQty - 1) / ordQty);
                var totalOutstandingMultiple = ((totalOutstandingQty + ordQty - 1) / ordQty);
                var multipleForQty = todayOutstandingMultiple;

                var markDownPct = buyMarkdownFromLcpDefault + (pctExtraMarkdownForAveraging * todayOutstandingMultiple);
                var calculatedToBuyPrice = Math.Round(0.9999 * (1 - markDownPct) * lastPriceToCompareWith, 1);

                // if CalculatedPriceToBuy is less than calculated from Holdingprice based calc then use totalOutstandingMultiple for Qty
                if (holdingOutstandingQty > 0)
                {
                    var markDownPctExpected = buyMarkdownFromLcpDefault + (pctExtraMarkdownForAveraging * totalOutstandingMultiple);
                    var priceExpected = Math.Round(0.9999 * (1 - markDownPctExpected) * holdingOutstandingPrice, 1);

                    if (calculatedToBuyPrice < priceExpected)
                    {
                        multipleForQty = totalOutstandingMultiple;
                        qtyStrategy = "TotalOutstandingMultiple";
                    }
                }

                calculatedOrderQty = ordQty * (1 + multipleForQty);

                calculatedToBuyPrice = Math.Min(calculatedToBuyPrice, buyPriceCap);

                if (errCode == BrokerErrorCode.Success && (todayOutstandingQty == 0 || (placeBuyNoLtpCompare || (ltp <= calculatedToBuyPrice))))
                {
                    Trace(string.Format("LTP {0}, calculatedToBuyPrice {1}, lastPriceToCompareWith {2}, calculatedOrderQty {3}, placeBuyNoLtpCompare {4}, PriceStrategy {5}, QtyStrategy {6} ", 
                        ltp, calculatedToBuyPrice, lastPriceToCompareWith, calculatedOrderQty, placeBuyNoLtpCompare, priceStrategy, qtyStrategy));

                    errCode = PlaceEquityOrder(exchStr, stockCode, OrderDirection.BUY, OrderPriceType.LIMIT, calculatedOrderQty, orderType, calculatedToBuyPrice, out todayOutstandingBuyOrderId, out upstoxOrderStatus);
                }
            }
        }

        public override void StockBuySell()
        {
            try
            {
                Init(AlgoType.AverageTheBuyThenSell);
            }
            catch (Exception ex)
            {
                Trace("Error:" + ex.Message + "\nStacktrace:" + ex.StackTrace);
                throw;
            }

            while (MarketUtils.IsMarketOpen())
            {
                try
                {
                    {
                        var newTrades = new Dictionary<string, EquityTradeBookRecord>();
                        // refresh trade book
                        errCode = myUpstoxWrapper.GetTradeBook(true, stockCode, out newTrades);

                        foreach (var tradeKv in newTrades.OrderBy(t => t.Value.Direction).Reverse())
                        {
                            var tradeRef = tradeKv.Key;
                            var trade = tradeKv.Value;

                            Trace(string.Format(tradeTraceFormat, stockCode, trade.Direction == OrderDirection.BUY ? "bought" : "sold", trade.NewQuantity, trade.Price,
                                holdingOrder.OrderId == tradeRef ? "CASH" : "MARGIN", trade.OrderId));

                            // if any holding sell executed
                            ProcessHoldingSellOrderExecution(newTrades);

                            // if SELL executed, then update today outstanding with executed qty (handle part executions using NewQuantity)
                            // If it is after 3.15 and broker did auto sq off, then broker's order ref is not with us and wont match with our sq off order. Our sqoff order will be cancelled by the broker
                            if (tradeRef == todayOutstandingSellOrderId || (MarketUtils.IsTimeAfter315() && trade.EquityOrderType == EquityOrderType.MARGIN))
                            {
                                todayOutstandingQty -= trade.NewQuantity;

                                if (todayOutstandingQty == 0)
                                {
                                    todayOutstandingPrice = 0;
                                    todayOutstandingSellOrderId = "";
                                }
                            }

                            // if BUY executed, then place a corresponding updated sell order. 
                            if (tradeRef == todayOutstandingBuyOrderId)
                            {
                                Dictionary<string, EquityOrderBookRecord> orders;
                                errCode = myUpstoxWrapper.GetOrderBook(false, false, stockCode, out orders);

                                if (orders[todayOutstandingBuyOrderId].Status == OrderStatus.COMPLETED)
                                {
                                    Trace(string.Format("Fully executed newqty {0} todayoutstandingqty {1} todayoutstandingprice {2} sellorderref {3} buyorderef {4} buyorderstatus {5}", trade.NewQuantity, todayOutstandingQty, todayOutstandingPrice, todayOutstandingSellOrderId, todayOutstandingBuyOrderId, orders[todayOutstandingBuyOrderId].Status));
                                    todayOutstandingBuyOrderId = "";
                                }
                                else
                                {
                                    Trace(string.Format("Partially executed newqty {0} todayoutstandingqty {1} todayoutstandingprice {2} sellorderref {3} buyorderef {4} buyorderstatus {5}", trade.NewQuantity, todayOutstandingQty, todayOutstandingPrice, todayOutstandingSellOrderId, todayOutstandingBuyOrderId, orders[todayOutstandingBuyOrderId].Status));
                                }

                                // update outstanding qty and outstanding price to place updated sell order
                                todayOutstandingPrice = (todayOutstandingPrice * todayOutstandingQty) + (trade.NewQuantity * trade.Price);
                                todayOutstandingQty += trade.NewQuantity;
                                if (todayOutstandingQty == 0)
                                    todayOutstandingPrice = 0;
                                else
                                    todayOutstandingPrice /= todayOutstandingQty;
                                todayOutstandingPrice = Math.Round(todayOutstandingPrice, 2);

                                if (todayOutstandingQty >= maxTodayOutstandingQtyAllowed)
                                    Trace(string.Format("TodayOutstandingQty reached the max. todayOutstandingQty: {0} maxTodayOutstandingQtyAllowed: {1}", todayOutstandingQty, maxTodayOutstandingQtyAllowed));

                                if ((todayOutstandingQty + holdingOutstandingQty) >= maxTotalOutstandingQtyAllowed)
                                    Trace(string.Format("TotalOutstandingQty reached the max. todayOutstandingQty: {0} holdingOutstandingQty: {1} maxTotalOutstandingQtyAllowed: {2}", todayOutstandingQty, holdingOutstandingQty, maxTotalOutstandingQtyAllowed));

                                settlementNumber = trade.SettlementNumber;

                                lastBuyPrice = useAvgBuyPriceInsteadOfLastBuyPriceToCalculateBuyPriceForNewOrder ? todayOutstandingPrice : trade.Price;

                                if (!string.IsNullOrEmpty(todayOutstandingSellOrderId))
                                {
                                    // cancel existing sell order if it exists
                                    errCode = CancelEquityOrder("[Buy Executed]", ref todayOutstandingSellOrderId, orderType, OrderDirection.SELL);
                                }
                                if (errCode == BrokerErrorCode.Success || string.IsNullOrEmpty(todayOutstandingSellOrderId))
                                {
                                    // place new sell order if previous cancelled or it was first one, update sell order ref
                                    var sellPrice = GetSellPrice(todayOutstandingPrice, false, false);
                                    errCode = PlaceEquityOrder(exchStr, stockCode, OrderDirection.SELL, OrderPriceType.LIMIT, todayOutstandingQty, orderType, sellPrice, out todayOutstandingSellOrderId, out upstoxOrderStatus);
                                }
                            }
                        }

                        PlaceBuyOrderIfEligible();
                    }

                    // Only relevant near EOD
                    TrySquareOffNearEOD(AlgoType.AverageTheBuyThenSell);
                }
                catch (Exception ex)
                {
                    Trace("Error:" + ex.Message + "\nStacktrace:" + ex.StackTrace);
                }

                if (MarketUtils.IsTimeAfter3XMin(0))
                {
                    // check if position conversion is already manually done then cancel the Margin order and update position file


                }

                if (MarketUtils.IsTimeAfter3XMin(28))
                {
                    CancelHoldingSellOrders();
                    ConvertToDeliveryAndUpdatePositionFile();
                }

                // update stats
                var buyValueToday = todayOutstandingPrice * (todayOutstandingQty + ordQty);
                pnlStats.maxBuyValueToday = Math.Max(pnlStats.maxBuyValueToday, buyValueToday);

                PauseBetweenTradeBookCheck();
            }

            // for safety call conversion once more if the conversion call in the above loop was missed due to Pause and loop's time check
            ConvertToDeliveryAndUpdatePositionFile(true); // EOD final update
        }

        // TODO: refactor.. not being used currently
        public new double GetBuyPrice(double ltp, bool isTodayFirstOrder, bool doesHoldingPositionExist)
        {
            var calculatedBuyPrice = base.GetBuyPrice(ltp, isTodayFirstOrder, doesHoldingPositionExist);

            var finalPrice = Math.Min(calculatedBuyPrice, holdingOutstandingPrice);

            if (holdingOutstandingQty > 0)
                finalPrice = Math.Min(finalPrice, holdingOutstandingPrice);

            return Math.Min(calculatedBuyPrice, buyPriceCap);
        }
    }
}
