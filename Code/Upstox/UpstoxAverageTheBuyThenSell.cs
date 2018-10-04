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
            if (string.IsNullOrEmpty(todayOutstandingBuyOrderId)
                && (todayOutstandingQty + holdingOutstandingQty) < maxTotalOutstandingQtyAllowed
                && todayOutstandingQty < maxTodayOutstandingQtyAllowed)
            {
                double ltp;
                var errCode = GetLTP(out ltp);
                var calculatedOrderQty = baseOrderQty;

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
                else if (lastBuyPrice > 0)
                {
                    lastPriceToCompareWith = lastBuyPrice;
                    priceStrategy = "LastBuyPriceAsLastFallback Markdown";
                }
                else
                {
                    Trace(string.Format("LTP:{0}, LastBuyPrice:{1}, HoldingOutstandingPrice:{2}. No reference price to place BUY order", ltp, lastBuyPrice, holdingOutstandingPrice));
                    return;
                }

                // price calc independent of the holdings
                var todayOutstandingTradeBucketNumberForPrice = Math.Min(todayOutstandingTradeCount, priceBucketsForPrice.Length - 1);
                var tighteningScale = 5;
                var markDownExtraPctCalculated = markDownPctForAveraging * (Math.Min(todayOutstandingTradeCount, tighteningScale) / tighteningScale);
                var priceBucketAgressionForPrice = priceBucketsForPrice[todayOutstandingTradeBucketNumberForPrice];
                var markDownPctCalculated = (markDownPctForBuy + markDownExtraPctCalculated) * priceBucketAgressionForPrice;
                var calculatedToBuyPrice = Math.Round((1 - markDownPctCalculated) * lastPriceToCompareWith, 1);

                priceStrategy = string.Format(
                    @"{0},LTP={1}, lastBuyPrice={2}, holdingOutstandingPrice={3}, lastPriceToCompareWith={4}, todayOutstandingTradeCount={5}, 
                    todayOutstandingTradeBucketNumberForPrice={6}, priceBucketAgressionForPrice={7}, markDownPctForBuy={8}, markDownPctForAveraging={9}, markDownExtraPctCalculated={10},
                    markDownPctCalculated={11}, calculatedToBuyPrice={12}, priceBucketsForPrice={13};",
                    priceStrategy, ltp, lastBuyPrice, holdingOutstandingPrice, lastPriceToCompareWith, todayOutstandingTradeCount, todayOutstandingTradeBucketNumberForPrice,
                    priceBucketAgressionForPrice, markDownPctForBuy, markDownPctForAveraging, markDownExtraPctCalculated, markDownPctCalculated, calculatedToBuyPrice, string.Join(":", priceBucketsForPrice));

                // Qty calc depends upon calculatedbuyprice and totaloutstanding qty
                var totalOutstandingQty = todayOutstandingQty + holdingOutstandingQty;
                double todayOutstandingMultiple = todayOutstandingQty / baseOrderQty;
                double totalOutstandingMultiple = totalOutstandingQty / baseOrderQty;
                var qtyFactorCalcForQty = totalOutstandingMultiple * qtyAgressionFactor;

                var totalAvgHoldingPrice = (todayOutstandingPrice * todayOutstandingQty) + (holdingOutstandingQty * holdingOutstandingPrice);
                totalAvgHoldingPrice = totalOutstandingQty > 0 ? totalAvgHoldingPrice / totalOutstandingQty : 0;
                var priceToCompareForQty = Math.Max(Math.Max(totalAvgHoldingPrice, lastBuyPrice), ltp);
                var priceDiffPct = (priceToCompareForQty - calculatedToBuyPrice) / priceToCompareForQty;
                var priceDiffMultiple = priceDiffPct / priceBucketWidthInPctForQty;
                var priceDiffBucketNumberForQty = Math.Min((int)priceDiffMultiple, priceBucketsForQty.Length - 1);
                var priceDiffBucketAgressionForQty = priceBucketsForQty[priceDiffBucketNumberForQty];
                var priceFactorCalcForQty = priceDiffMultiple * priceDiffBucketAgressionForQty;

                var qtyCurve = qtyFactorCalcForQty + priceFactorCalcForQty;

                calculatedOrderQty = (int)(baseOrderQty * Math.Max(1, qtyCurve));

                calculatedOrderQty = Math.Min(calculatedOrderQty, maxTodayOutstandingQtyAllowed - todayOutstandingQty);
                calculatedOrderQty = Math.Min(calculatedOrderQty, maxTotalOutstandingQtyAllowed - totalOutstandingQty);

                qtyStrategy = string.Format(
                    @"todayOutstandingQty={0}, todayOutstandingPrice={1}, holdingOutstandingQty={2}, holdingOutstandingPrice={3}, baseOrderQty={4}, todayOutstandingMultiple={5}, totalOutstandingMultiple={6}, 
                    qtyAgressionFactor={7}, qtyFactorCalcForQty={8};
                    totalAvgHoldingPrice={9}, priceToCompareForQty={10}, priceDiffPct={11}, priceBucketWidthInPctForQty={12}, priceDiffMultiple={13},
                    priceDiffBucketNumberForQty={14}, priceDiffBucketAgressionForQty={15}, priceFactorCalcForQty={16};
                    qtyCurve={17}, calculatedOrderQty={18}, maxTodayPositionValueMultiple={19}, maxTotalPositionValueMultiple={20};",
                    todayOutstandingQty, Math.Round(todayOutstandingPrice, 2), holdingOutstandingQty, Math.Round(holdingOutstandingPrice, 2), baseOrderQty, todayOutstandingMultiple, totalOutstandingMultiple,
                    qtyAgressionFactor, qtyFactorCalcForQty,
                    Math.Round(totalAvgHoldingPrice, 2), Math.Round(priceToCompareForQty, 2), Math.Round(priceDiffPct, 2), priceBucketWidthInPctForQty, Math.Round(priceDiffMultiple, 2),
                    priceDiffBucketNumberForQty, priceDiffBucketAgressionForQty, Math.Round(priceFactorCalcForQty, 2),
                    Math.Round(qtyCurve, 3), calculatedOrderQty, maxTodayOutstandingQtyAllowed, maxTotalOutstandingQtyAllowed);

                if (errCode == BrokerErrorCode.Success && (todayOutstandingQty == 0 || (placeBuyNoLtpCompare || (ltp <= calculatedToBuyPrice))))
                {
                    Trace(string.Format("\nPriceStrategy~{0}\nQtyStrategy~{1}\nplaceBuyNoLtpCompare~{2}", priceStrategy, qtyStrategy, placeBuyNoLtpCompare));

                    errCode = PlaceEquityOrder(exchStr, stockCode, OrderDirection.BUY, OrderPriceType.LIMIT, calculatedOrderQty, orderType, calculatedToBuyPrice, out todayOutstandingBuyOrderId, out upstoxOrderStatus);

                    if (errCode == BrokerErrorCode.Success)
                        lastBuyOrdQty = calculatedOrderQty;
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
                        var isSellOnlyExecutedAndFully = false;
                        var newTrades = new Dictionary<string, EquityTradeBookRecord>();
                        // refresh trade book
                        errCode = myUpstoxWrapper.GetTradeBook(true, stockCode, out newTrades);

                        foreach (var tradeKv in newTrades.OrderBy(t => t.Value.Direction).Reverse())
                        {
                            var tradeRef = tradeKv.Key;
                            var trade = tradeKv.Value;

                            Trace(string.Format(tradeTraceFormat, stockCode, trade.Direction == OrderDirection.BUY ? "bought" : "sold", trade.NewQuantity, trade.Price,
                                holdingOrder.OrderId == tradeRef ? "DELIVERY" : "MARGIN", trade.OrderId));

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
                                    todayOutstandingTradeCount = 0;
                                    isSellOnlyExecutedAndFully = true;
                                }
                            }

                            // if BUY executed, then place a corresponding updated sell order. 
                            if (tradeRef == todayOutstandingBuyOrderId)
                            {
                                Dictionary<string, EquityOrderBookRecord> orders;
                                errCode = myUpstoxWrapper.GetOrderBook(false, false, stockCode, out orders);

                                if (orders[todayOutstandingBuyOrderId].Status == OrderStatus.COMPLETED)
                                {
                                    Trace(string.Format("[Trade Execution] Fully executed newqty {0} todayoutstandingqty {1} todayoutstandingprice {2} sellorderref {3} buyorderef {4} buyorderstatus {5}", trade.NewQuantity, todayOutstandingQty, todayOutstandingPrice, todayOutstandingSellOrderId, todayOutstandingBuyOrderId, orders[todayOutstandingBuyOrderId].Status));
                                    todayOutstandingBuyOrderId = "";
                                    todayOutstandingTradeCount++;
                                }
                                else
                                {
                                    Trace(string.Format("[Trade Execution] Partially executed newqty {0} todayoutstandingqty {1} todayoutstandingprice {2} sellorderref {3} buyorderef {4} buyorderstatus {5}", trade.NewQuantity, todayOutstandingQty, todayOutstandingPrice, todayOutstandingSellOrderId, todayOutstandingBuyOrderId, orders[todayOutstandingBuyOrderId].Status));
                                }

                                // update outstanding qty and outstanding price to place updated sell order
                                todayOutstandingPrice = (todayOutstandingPrice * todayOutstandingQty) + (trade.NewQuantity * trade.Price);
                                todayOutstandingQty += trade.NewQuantity;
                                todayOutstandingPrice = todayOutstandingQty == 0 ? 0 : todayOutstandingPrice / todayOutstandingQty;
                                //todayOutstandingPrice = Math.Round(todayOutstandingPrice, 2);

                                if (todayOutstandingQty >= maxTodayOutstandingQtyAllowed)
                                    Trace(string.Format("TodayOutstandingQty reached the max. todayOutstandingQty: {0} maxTodayPositionValueMultiple: {1}", todayOutstandingQty, maxTodayOutstandingQtyAllowed));

                                if ((todayOutstandingQty + holdingOutstandingQty) >= maxTotalOutstandingQtyAllowed)
                                    Trace(string.Format("TotalOutstandingQty reached the max. todayOutstandingQty: {0} holdingOutstandingQty: {1} maxTotalPositionValueMultiple: {2}", todayOutstandingQty, holdingOutstandingQty, maxTotalOutstandingQtyAllowed));

                                lastBuyPrice = trade.Price;

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

                                isSellOnlyExecutedAndFully = false;
                            }
                        }

                        if (isSellOnlyExecutedAndFully)
                        {
                            // Cancel existing Buy order which might be an average seeking order, as the pricecalc might have changed

                            if (!string.IsNullOrEmpty(todayOutstandingBuyOrderId))
                            {
                                // cancel existing buy order
                                errCode = CancelEquityOrder("[Sell Executed Fully]", ref todayOutstandingBuyOrderId, orderType, OrderDirection.BUY);
                            }
                        }

                        PlaceBuyOrderIfEligible();
                    }

                    HandleConversionAnytime();
                    TrySquareOffNearEOD();
                }
                catch (Exception ex)
                {
                    Trace("Error:" + ex.Message + "\nStacktrace:" + ex.StackTrace);
                }

                if (MarketUtils.IsTimeAfter3XMin(29))
                    CancelOpenOrders();

                // update stats
                var buyValueToday = todayOutstandingPrice * (todayOutstandingQty + lastBuyOrdQty);
                pnlStats.maxBuyValueToday = Math.Max(pnlStats.maxBuyValueToday, buyValueToday);

                PauseBetweenTradeBookCheck();
            }

            EODProcess(true); // EOD final update
        }
    }
}
