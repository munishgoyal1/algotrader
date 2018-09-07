using StockTrader.Core;
using StockTrader.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleTrader
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
            // start and end timings
            if (!IsOrderTimeWithinRange())
                return;

            // place buy order if eligible: if there is no pending buy order and if totaloutstanding qty is less than maxoutstanding
            if (string.IsNullOrEmpty(todayOutstandingBuyOrderId) && todayBuyOrderCount < maxBuyOrdersAllowedInADay
                && (todayOutstandingQty + holdingOutstandingQty) < maxTotalOutstandingQtyAllowed
                && todayOutstandingQty < maxTodayOutstandingQtyAllowed)
            {
                double ltp;
                var errCode = GetLTP(out ltp);

                // todayOutstandingQty == 0 default case
                var markDownPct = buyMarkdownFromLcpDefault;
                var lastPriceToCompareWith = ltp;

                double priceArrivedFromHolding = double.MaxValue;
                double priceArrivedFromTodayOutstanding = double.MaxValue;
                double priceArrivedFromLtpDefault = Math.Round(0.999 * (1 - markDownPct) * lastPriceToCompareWith, 1);
                double buyPrice = priceArrivedFromLtpDefault;
                var priceStrategy = "Default markdown from Ltp";

                if (holdingOutstandingQty > 0)
                {
                    markDownPct = buyMarkdownFromLcpDefault;// + pctExtraMarkdownForAveraging;
                    lastPriceToCompareWith = holdingOutstandingPrice;
                    priceArrivedFromHolding = Math.Round(0.999 * (1 - markDownPct) * lastPriceToCompareWith, 1);

                    if (priceArrivedFromHolding < buyPrice)
                    {
                        priceStrategy = "Average from Holding Price";
                        buyPrice = priceArrivedFromHolding;
                    }
                }

                if (todayOutstandingQty > 0)
                {
                    markDownPct = buyMarkdownFromLcpDefault + (pctExtraMarkdownForAveraging * todayBuyOrderCount);
                    lastPriceToCompareWith = lastBuyPrice;
                    priceArrivedFromTodayOutstanding = Math.Round(0.999 * (1 - markDownPct) * lastPriceToCompareWith, 1);

                    if (priceArrivedFromTodayOutstanding < buyPrice)
                    {
                        priceStrategy = "Average from Today's outstanding";
                        buyPrice = priceArrivedFromTodayOutstanding;
                    }
                }

                buyPrice = Math.Min(buyPrice, buyPriceCap);

                // if ltp is less than required price then place the order or if there is no outstanding today then place the order anyway
                if (errCode == BrokerErrorCode.Success && (todayOutstandingQty == 0 || ltp <= buyPrice))
                {
                    if (todayOutstandingQty != 0)
                        Trace(string.Format("LTP {0} is -{1}% of Last {3} price {2}", ltp, markDownPct * 100, lastPriceToCompareWith, priceStrategy));
                    else
                        Trace(string.Format("LTP {0} lastPriceToCompareWith {1} markDownPct {2} PriceStrategy {3}", ltp, lastPriceToCompareWith, markDownPct, priceStrategy));
                    // place buy order, update buy order ref
                    errCode = PlaceEquityOrder(exchStr, stockCode, OrderDirection.BUY, OrderPriceType.LIMIT, ordQty, orderType, buyPrice, out todayOutstandingBuyOrderId);
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
                    // Try to convert to delivery pending open positions (nearing expiry) from previous days
                    //ConvertToDeliveryPreviousDaysExpiringOpenPositions();

                    var holdingTradesRef = holdingsOrders.Select(h => h.OrderId);

                    {
                        var newTrades = new Dictionary<string, EquityTradeBookRecord>();
                        // refresh trade book
                        errCode = myUpstoxWrapper.GetTradeBook(true, stockCode, out newTrades);

                        foreach (var tradeKv in newTrades.OrderBy(t => t.Value.Direction).Reverse())
                        {
                            var tradeRef = tradeKv.Key;
                            var trade = tradeKv.Value;

                            Trace(string.Format(tradeTraceFormat, stockCode, trade.Direction == OrderDirection.BUY ? "bought" : "sold", trade.NewQuantity, trade.Price,
                                holdingTradesRef.Contains(tradeRef) ? "CASH" : "MARGIN"));

                            // if any holding sell executed
                            ProcessHoldingSellOrderExecution(newTrades);

                            // if sell executed, then update today outstanding to 0 , because sell order always contains the total outstanding qty and now all of it got sold with this trade
                            // but handle part executions using NewQuantity
                            if (tradeRef == todayOutstandingSellOrderId)
                            {
                                todayOutstandingQty -= trade.NewQuantity;

                                if (todayOutstandingQty == 0)
                                {
                                    todayOutstandingPrice = 0;
                                    todayOutstandingSellOrderId = "";
                                }
                            }

                            // if buy executed, then place a corresponding updated sell order. assumption is that qty is completely executed.
                            // part executed qty will get considered only for first part, for later ones there is no update because we wont get it in newTrades again
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
                                    errCode = PlaceEquityOrder(exchStr, stockCode, OrderDirection.SELL, OrderPriceType.LIMIT, todayOutstandingQty, orderType, sellPrice, out todayOutstandingSellOrderId);
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

                PauseBetweenTradeBookCheck();
            }

            // for safety call conversion once more if the conversion call in the above loop was missed due to Pause and loop's time check
            //CancelHoldingSellOrders();
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
