﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using StockTrader.Brokers.UpstoxBroker;
using StockTrader.Core;
using StockTrader.Platform.Logging;
using StockTrader.Utilities;
using UpstoxNet;
using UpstoxTrader;

namespace UpstoxTrader
{
    public class HoldingOrder
    {
        public int UnexecutedQty;
        public int StartingQty;
        public string OrderId;
        public OrderStatus Status;
    }

    public class UpstoxBuySellBase
    {
        public MyUpstoxWrapper myUpstoxWrapper = null;

        // Config
        public EquityOrderType orderType;
        public double markDownPctForBuy;
        public double markDownPctForAveraging;
        public double sellMarkup;
        public double deliveryBrokerage = 0.0035;
        public bool placeBuyNoLtpCompare;
        public DateTime startTime;
        public DateTime endTime;
        public string stockCode = null;
        public int baseOrdQty = 0;
        public int maxTotalOutstandingQtyAllowed = 0;
        public int maxTodayOutstandingQtyAllowed = 0;
        public Exchange exchange;
        public string exchStr;

        public string positionFile;

        // Algo core price calcs parameters
        public double priceBucketWidthForQty;
        public double[] priceBucketFactorForQty;
        public double qtyAgressionFactor;
        public double[] priceBucketFactorForPrice;

        // State
        public double holdingOutstandingPrice = 0;
        public int holdingOutstandingQty = 0;
        public double todayOutstandingPrice = 0;
        public int todayOutstandingQty = 0;
        public string todayOutstandingBuyOrderId = ""; // outstanding buy order Id
        public string todayOutstandingSellOrderId = "";// outstanding sell order Id
        public HoldingOrder holdingOrder = new HoldingOrder();
        public double lowerCircuitLimit;
        public double upperCircuitLimit;
        public int lastBuyOrdQty = 0;
        public double lastBuyPrice = 0;
        public bool isFirstBuyOrder = true;
        public int todayPositionCount = 0;
        public bool isEODMinProfitSquareOffLimitOrderUpdated = false;
        public bool isEODMinLossSquareOffMarketOrderUpdated = false;
        public bool isOutstandingPositionConverted = false;
        public double Ltp;
        public OrderStatus upstoxOrderStatus;

        public const string orderTraceFormat = "[Place Order {4}]: {5} {0} {1} {2} @ {3} {6}. OrderId = {7}. BrokerOrderStatus={8}";
        public const string orderCancelTraceFormat = "{0}: {1} {2} {3}: {4}";
        public const string tradeTraceFormat = "[Trade Execution] {4} Trade: {0} {1} {2} @ {3}. OrderId = {5}";
        public const string deliveryTraceFormat = "Conversion to delivery: {0} {1} qty of {2}";
        public const string positionFileTotalQtyPriceFormat = "{0} {1}";
        public const string positionFileLineQtyPriceFormat = "{0} {1} {2} {3}";

        public BrokerErrorCode errCode;
        public UpstoxPnLStats pnlStats = new UpstoxPnLStats();

        public virtual void StockBuySell()
        {

        }
        public UpstoxBuySellBase(UpstoxTradeParams tradeParams)
        {
            myUpstoxWrapper = tradeParams.upstox;
            tradeParams.stats = pnlStats;
            stockCode = tradeParams.stockCode;
            baseOrdQty = tradeParams.baseOrdQty;
            maxTotalOutstandingQtyAllowed = tradeParams.maxTotalOutstandingQtyAllowed;
            maxTodayOutstandingQtyAllowed = tradeParams.maxTodayOutstandingQtyAllowed;
            exchange = tradeParams.exchange;
            exchStr = exchange == Exchange.NSE ? "NSE_EQ" : "BSE_EQ";
            positionFile = AlgoUtils.GetPositionFile(stockCode);

            orderType = tradeParams.orderType;
            markDownPctForBuy = tradeParams.markDownPctForBuy;
            markDownPctForAveraging = tradeParams.markDownPctForAveraging;
            sellMarkup = tradeParams.sellMarkup;
            placeBuyNoLtpCompare = tradeParams.placeBuyNoLtpCompare;
            startTime = tradeParams.startTime;
            endTime = tradeParams.endTime;

            priceBucketWidthForQty = tradeParams.priceBucketWidthForQty;
            priceBucketFactorForQty = tradeParams.priceBucketFactorForQty;
            qtyAgressionFactor = tradeParams.qtyAgressionFactor;
            priceBucketFactorForPrice = tradeParams.priceBucketFactorForPrice;
        }

        public bool IsOrderTimeWithinRange()
        {
            var now = DateTime.Now;

            var r1 = TimeSpan.Compare(now.TimeOfDay, startTime.TimeOfDay);
            var r2 = TimeSpan.Compare(now.TimeOfDay, endTime.TimeOfDay);

            if (r1 >= 0 && r2 <= 0)
                return true;

            return false;
        }

        public void QuoteReceived(object sender, QuotesReceivedEventArgs args)
        {

            if (stockCode == args.TrdSym)
                Interlocked.Exchange(ref Ltp, args.LTP);

            //if (stockAlgos.Contains(args.TrdSym))//
            //{
            //    var algo = stockAlgos[args.TrdSym];
            //    Interlocked.Exchange(algo.Ltp, args.LTP);
            //}
            // Console.WriteLine(string.Format("Sym {0}, LTP {1}, LTT {2}", args.TrdSym, args.LTP, args.LTT));

        }


        // reads position file: qty avgprice holdingssellorderref
        // gets order and trade book
        // if holdings sell order is executed, then holding qty is updated to 0, and position file is updated with only today outstanding and empty sellorderref
        // holdings data is seperate and today outstanding data is seperate
        // single buy or sell outsanding order at any time is assumed
        // if there is no sell order for holdings then create a sell order and update position file with holdings and sellorderref
        // position file contains only the holding data along with the holding sell order ref. it does not have today's outstanding. Only at EOD if conversion to delivery is done then that is assumed to be holding and written to postion file
        // holding sell order if reqd is placed only once at start in Init, not in loop run. that time iffordeliveryflag is set so that sell price is calculated as per deliverymarkup parameter
        // in normal run , sell orders are placed as per normal marginmarkup parameter
        // today's outstanding is derived from the tradebook and outstanding orders derived from order book. no state is saved outside. day before holdings are present in positions file
        public void Init(AlgoType algoType)
        {
            if (!File.Exists(positionFile))
                File.WriteAllText(positionFile, "");

            // Position file always contains the holding qty, holding price and different type of holdings' details (demat/btst, qty, sell order ref) etc
            ReadPositionFile();

            // populate stats
            pnlStats.prevHoldingPrice = holdingOutstandingPrice;
            pnlStats.prevHoldingQty = holdingOutstandingQty;

            // Get circuit prices
            EquitySymbolQuote quote;
            errCode = myUpstoxWrapper.GetSnapQuote(exchStr, stockCode, out quote);

            if (errCode == BrokerErrorCode.Success)
            {
                lowerCircuitLimit = quote.LowerCircuitPrice;
                upperCircuitLimit = quote.UpperCircuitPrice;
            }


            GetLTPOnDemand(out Ltp);

            myUpstoxWrapper.Upstox.QuotesReceivedEvent += new UpstoxNet.Upstox.QuotesReceivedEventEventHandler(QuoteReceived);

            var substatus = myUpstoxWrapper.Upstox.SubscribeQuotes(exchStr, stockCode);

            var orders = new Dictionary<string, EquityOrderBookRecord>();
            var trades = new Dictionary<string, EquityTradeBookRecord>();

            // latest (wrt time) trades or orders appear at start of the output
            errCode = myUpstoxWrapper.GetTradeBook(false, stockCode, out trades);

            if (errCode != BrokerErrorCode.Success)
                throw new Exception("Failed getting TradeBook at Init");

            errCode = myUpstoxWrapper.GetOrderBook(false, true, stockCode, out orders);

            if (errCode != BrokerErrorCode.Success)
                throw new Exception("Failed getting OrderBook at Init");

            // any outstanding qty (buy minus sell trades) today except from holding qty trade
            var onlyTodayBuyQty = trades.Values.Where(t => t.Direction == OrderDirection.BUY && t.EquityOrderType == orderType).Sum(t => t.Quantity);
            var onlyTodaySellQty = trades.Values.Where(t => t.Direction == OrderDirection.SELL && t.EquityOrderType == orderType).Sum(t => t.Quantity);

            // For delivery ordertype (eg CAPLIN) find the executed qty and remove it from todayoutstanding
            if (!string.IsNullOrEmpty(holdingOrder.OrderId) && orderType == EquityOrderType.DELIVERY)
            {
                onlyTodaySellQty = onlyTodaySellQty - (holdingOrder.StartingQty - holdingOrder.UnexecutedQty);
            }

            todayOutstandingQty = onlyTodayBuyQty - onlyTodaySellQty;
            todayOutstandingQty = Math.Max(todayOutstandingQty, 0);

            var buyTrades = trades.Values.Where(t => t.Direction == OrderDirection.BUY).OrderByDescending(t => t.DateTime).ToList();

            //sort the trade book and get last buy price
            lastBuyPrice = buyTrades.Any() ? buyTrades.First().Price : holdingOutstandingPrice;

            var qtyTotal = 0;
            int outstandingAttritbutionOrderCount = 0;
            while (qtyTotal < todayOutstandingQty)
            {
                qtyTotal += (baseOrdQty * ++outstandingAttritbutionOrderCount);
            }

            // these are latest trades taken. each buy trade is for single lot and thus for each lot there is a trade
            todayOutstandingPrice = outstandingAttritbutionOrderCount == 0 ? 0 : buyTrades.Take(outstandingAttritbutionOrderCount).Average(t => t.Price);

            ProcessHoldingSellOrderExecution(trades);

            var buyOrders = orders.Values.Where(o => o.Direction == OrderDirection.BUY && o.Status == OrderStatus.ORDERED);
            var sellOrders = orders.Values.Where(o => o.Direction == OrderDirection.SELL && o.Status == OrderStatus.ORDERED && holdingOrder.OrderId != o.OrderId); // pick only today's outstanding related sell orders

            // assumed that there is always at max only single outstanding buy order and a at max single outstanding sell order
            todayOutstandingBuyOrderId = buyOrders.Any() ? buyOrders.First().OrderId : "";
            todayOutstandingSellOrderId = sellOrders.Any() ? sellOrders.First().OrderId : "";

            var sellOrder = sellOrders.Any() ? sellOrders.First() : null;

            isFirstBuyOrder = string.IsNullOrEmpty(todayOutstandingBuyOrderId) && !trades.Where(t => t.Value.Direction == OrderDirection.BUY).Any();

            // if sqoff sell order for holding is needed then place it 
            if (holdingOutstandingQty > 0 && string.IsNullOrEmpty(holdingOrder.OrderId))
            {
                // get holdings
                List<EquityDematHoldingRecord> dematHoldings;
                errCode = myUpstoxWrapper.GetHoldings(stockCode, out dematHoldings);

                if (errCode != BrokerErrorCode.Success)
                    throw new Exception("Failed getting Holdings at Init");

                // place sq off sell order, update sell order ref
                var sellPrice = GetSellPrice(holdingOutstandingPrice, true, false);

                if (dematHoldings.Any())
                {
                    var dematHolding = dematHoldings.First();
                    if (dematHolding.BlockedQuantity < holdingOutstandingQty)
                    {
                        var algoPendingQty = holdingOutstandingQty - dematHolding.BlockedQuantity;
                        errCode = PlaceEquityOrder(exchStr, stockCode, OrderDirection.SELL, OrderPriceType.LIMIT, algoPendingQty, EquityOrderType.DELIVERY, sellPrice, out holdingOrder.OrderId, out upstoxOrderStatus);

                        if (errCode == BrokerErrorCode.Success)
                        {
                            holdingOrder.UnexecutedQty = algoPendingQty;
                            holdingOrder.Status = OrderStatus.ORDERED;
                        }
                    }
                }

                UpdatePositionFile();
            }

            // For AverageTheBuyThenSell algo - place sell order for outstanding qty if not already present
            if (algoType == AlgoType.AverageTheBuyThenSell)
            {
                // check if the outstanding sell order has matching qty or not
                if (sellOrder != null && !string.IsNullOrEmpty(todayOutstandingSellOrderId) && sellOrder.Quantity < todayOutstandingQty && todayOutstandingQty != int.MaxValue)
                {
                    // Cancel existing sell order 
                    errCode = CancelEquityOrder("[Init Update Sell Qty]", ref todayOutstandingSellOrderId, orderType, OrderDirection.SELL);
                }

                if (string.IsNullOrEmpty(todayOutstandingSellOrderId) && todayOutstandingQty > 0)
                {
                    var sellPrice = GetSellPrice(todayOutstandingPrice, false, false);
                    errCode = PlaceEquityOrder(exchStr, stockCode, OrderDirection.SELL, OrderPriceType.LIMIT, todayOutstandingQty, orderType, sellPrice, out todayOutstandingSellOrderId, out upstoxOrderStatus);
                }
            }

            todayPositionCount = buyOrders.Count();
        }

        protected void ProcessHoldingSellOrderExecution(Dictionary<string, EquityTradeBookRecord> trades)
        {
            if (holdingOrder.UnexecutedQty > 0 && !string.IsNullOrEmpty(holdingOrder.OrderId))
            {
                if (trades.ContainsKey(holdingOrder.OrderId))
                {
                    var trade = trades[holdingOrder.OrderId];
                    holdingOutstandingQty -= trade.NewQuantity;
                    holdingOrder.UnexecutedQty -= trade.NewQuantity;

                    holdingOutstandingQty = Math.Max(holdingOutstandingQty, 0);
                    holdingOrder.UnexecutedQty = Math.Max(holdingOrder.UnexecutedQty, 0);

                    if (holdingOrder.UnexecutedQty == 0)
                        // instead of removing the order mark the status
                        holdingOrder.Status = OrderStatus.COMPLETED;
                    else
                        holdingOrder.Status = OrderStatus.PARTEXEC;

                }
            }

            if (holdingOutstandingQty == 0)
                holdingOutstandingPrice = 0;

            UpdatePositionFile();
        }

        public BrokerErrorCode CancelEquityOrder(string source, ref string orderId, EquityOrderType type, OrderDirection side)
        {
            var errCode = myUpstoxWrapper.CancelOrder(orderId);

            Trace(string.Format(orderCancelTraceFormat, source, stockCode, side + " order cancel", ", OrderRef = " + orderId, errCode));

            if (errCode == BrokerErrorCode.Success)
                orderId = "";
            return errCode;
        }

        public double GetSellPrice(double price, bool isForDeliveryQty, bool isForMinProfitSquareOff)
        {
            var factor = sellMarkup;

            if (isForDeliveryQty)
                factor = sellMarkup + deliveryBrokerage;

            if (isForMinProfitSquareOff)
                factor = 1 + deliveryBrokerage;

            return Math.Round(factor * price, 1);
        }

        public BrokerErrorCode GetLTP(out double ltp)
        {
            BrokerErrorCode errCode = BrokerErrorCode.Success;
            ltp = Ltp;
            return errCode;
        }

        public BrokerErrorCode GetLTPOnDemand(out double ltp)
        {
            EquitySymbolQuote[] quote;
            BrokerErrorCode errCode = BrokerErrorCode.Unknown;
            int retryCount = 0;
            DateTime lut;
            ltp = 0.0;

            while (errCode != BrokerErrorCode.Success && retryCount++ < 3)
                try
                {
                    errCode = myUpstoxWrapper.GetEquityLTP(exchStr, stockCode, out ltp, out lut);
                    if (MarketUtils.IsMarketOpen())
                    {
                        if (DateTime.Now - lut >= TimeSpan.FromMinutes(5))
                            ltp = 0;
                    }
                }
                catch (Exception ex)
                {
                    Trace("Error:" + ex.Message + "\nStacktrace:" + ex.StackTrace);
                    if (retryCount >= 3)
                        break;
                }

            return errCode;
        }

        public void Trace(string message)
        {
            message = GetType().Name + " " + stockCode + " " + message;
            Console.WriteLine(DateTime.Now.ToString() + " " + message);
            FileTracing.TraceOut(message);
        }

        public BrokerErrorCode PlaceEquityOrder(
            string exchange,
            string stockCode,
            OrderDirection orderDirection,
            OrderPriceType orderPriceType,
            int quantity,
            EquityOrderType orderType,
            double price,
            out string orderId,
            out OrderStatus orderStatus)
        {
            BrokerErrorCode errCode = BrokerErrorCode.Unknown;
            orderStatus = OrderStatus.UNKNOWN;
            orderId = "";

            if (orderPriceType == OrderPriceType.LIMIT && (price < lowerCircuitLimit || price > upperCircuitLimit))
            {
                Trace(string.Format("orderPrice {0} is outside circuit limits of lowerCircuitLimit {1} upperCircuitLimit {2}, Not placing order", price, lowerCircuitLimit, upperCircuitLimit));
                return BrokerErrorCode.OutsidePriceRange;
            }

            if (quantity <= 0)
            {
                Trace(string.Format("quantity {0} is invalid, Not placing order", quantity));
                return BrokerErrorCode.InvalidLotSize;
            }


            errCode = myUpstoxWrapper.PlaceEquityOrder(exchange, stockCode, orderDirection, orderPriceType, quantity, orderType, price, out orderId, out orderStatus);

            Trace(string.Format(orderTraceFormat, stockCode, orderDirection, quantity, price, orderType, errCode, orderPriceType, orderId, orderStatus));

            return errCode;
        }

        public void ReadPositionFile()
        {
            var lines = File.ReadAllLines(positionFile);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!string.IsNullOrEmpty(line))
                {
                    var split = line.Split(' ');
                    if (i == 0)
                    {
                        holdingOutstandingQty = int.Parse(split[0].Trim());
                        holdingOutstandingPrice = double.Parse(split[1].Trim());
                        holdingOrder.StartingQty = holdingOutstandingQty;
                        holdingOrder.UnexecutedQty = holdingOrder.StartingQty;
                        holdingOrder.Status = OrderStatus.NOTFOUND;
                        holdingOrder.OrderId = null;
                    }
                    else
                    {
                        holdingOrder.StartingQty = int.Parse(split[0].Trim());
                        holdingOrder.UnexecutedQty = int.Parse(split[1].Trim());
                        holdingOrder.OrderId = split[2].Trim();
                        holdingOrder.Status = (OrderStatus)Enum.Parse(typeof(OrderStatus), split[3].Trim());
                    }
                }
            }
        }

        public void UpdatePositionFile(bool isEODUpdate = false)
        {
            var lines = new string[2];
            if (holdingOutstandingQty < 0)
            {
                Trace(string.Format("HoldingOutstandingQty is invalid @ {0}. Setting it to 0.", holdingOutstandingQty));
                holdingOutstandingQty = 0;
                holdingOutstandingPrice = 0;
            }

            lines[0] = string.Format(positionFileTotalQtyPriceFormat, holdingOutstandingQty, holdingOutstandingPrice);

            if (!isEODUpdate)
                lines[1] = string.Format(positionFileLineQtyPriceFormat, holdingOrder.StartingQty, holdingOrder.UnexecutedQty, holdingOrder.OrderId, holdingOrder.Status);

            File.WriteAllLines(positionFile, lines);

            if (isEODUpdate)
                Trace(string.Format("{2}PositionFile updated as {0} {1}", holdingOutstandingQty, holdingOutstandingPrice, isEODUpdate ? "EOD " : ""));
        }

        public void HandleConversionAnytime()
        {
            var eventType = "[Converted to DELIVERY]";
            string strategy = "[Converted to DELIVERY]";
            var ordPriceType = OrderPriceType.LIMIT;
            var equityOrderType = EquityOrderType.DELIVERY;

            if (!(todayOutstandingQty > 0 && !isOutstandingPositionConverted && orderType == EquityOrderType.MARGIN))
                return;

            // Assuming the position and the sqoff order are same qty (i.e. in sync as of now)
            // for already DELIVERY type sq off order, no need to do anything. Either this logic has already run or the stock was started in DELIVERY mode from starting itself
            List<EquityPositionRecord> positions;
            errCode = myUpstoxWrapper.GetPositions(stockCode, out positions);

            if (errCode == BrokerErrorCode.Success)
            {
                var position = positions.Where(p => p.Exchange == exchStr && p.EquityOrderType == EquityOrderType.DELIVERY && p.BuyQuantity > 0).FirstOrDefault();

                // check if position is converted to delivery, then cancel sq off order and place sell delivery order
                // match correct converted position.
                if (position != null)
                {
                    strategy = string.Format("{0}: Detected position conversion. Convert SELL order to DELIVERY", eventType);
                    isOutstandingPositionConverted = true;
                }
            }

            if (isOutstandingPositionConverted && !string.IsNullOrEmpty(todayOutstandingBuyOrderId))
            {
                // cancel existing buy order
                errCode = CancelEquityOrder(eventType, ref todayOutstandingBuyOrderId, orderType, OrderDirection.BUY);
            }

            if (isOutstandingPositionConverted)
            {
                // bought qty needs square off. there is outstanding sell order, revise the price to try square off 
                if (!string.IsNullOrEmpty(todayOutstandingSellOrderId))
                {
                    Trace(strategy);
                    // cancel existing sell order
                    errCode = CancelEquityOrder(eventType, ref todayOutstandingSellOrderId, orderType, OrderDirection.SELL);

                    if (errCode == BrokerErrorCode.Success)
                    {
                        // place new sell order, update sell order ref
                        var sellPrice = GetSellPrice(todayOutstandingPrice, false, true);
                        errCode = PlaceEquityOrder(exchStr, stockCode, OrderDirection.SELL, ordPriceType, todayOutstandingQty, equityOrderType, sellPrice, out todayOutstandingSellOrderId, out upstoxOrderStatus);
                    }
                }
            }
        }

        // Try min profit squareoff first between 
        // 3 - 3.05 = min profit limit sell order
        // 3.05 - 3.10 = min loss market order (if EOD sqoff enabled and loss within maxloss pct)
        //
        public void TrySquareOffNearEOD()
        {
            var eventType = "[EOD]";
            string strategy = "None";
            var ordPriceType = OrderPriceType.LIMIT;
            var equityOrderType = isOutstandingPositionConverted ? EquityOrderType.DELIVERY : orderType;
            var updateSellOrder = false;


            // just cancel the outstanding buy order Post 3PM
            if (MarketUtils.IsTimeAfter3XMin(0))
            {
                if (!string.IsNullOrEmpty(todayOutstandingBuyOrderId))
                {
                    // cancel existing buy order
                    errCode = CancelEquityOrder(eventType, ref todayOutstandingBuyOrderId, orderType, OrderDirection.BUY);
                }
            }

            if (todayOutstandingQty == 0)
                return;

            // if after 3 pm, then try to square off in at least no profit no loss if possible. cancel the outstanding buys anyway
            if (MarketUtils.IsTimeAfter3XMin(0))
            {
                // 3.05 - 3.10 pm time. market order type if must sqoff at EOD and given pct loss is within acceptable range and outstanding price is not a good price to keep holding
                if (MarketUtils.IsMinutesAfter3Between(5, 10) && !isEODMinLossSquareOffMarketOrderUpdated)
                {
                    double ltp;
                    var errCode = GetLTP(out ltp);

                    if (errCode != BrokerErrorCode.Success)
                        return;

                    var diff = Math.Round((ltp - todayOutstandingPrice) / ltp, 5);

                    if (diff > deliveryBrokerage)
                    {
                        strategy = string.Format("{5}: max loss {0}% is less than {1}% . LTP is {2}. Place squareoff @ MARKET.", diff, ltp, eventType);
                        ordPriceType = OrderPriceType.MARKET;
                        updateSellOrder = true;
                        isEODMinLossSquareOffMarketOrderUpdated = true;
                    }
                }

                // 3.00 - 3.05 pm time. try simple limit order with min profit price. watch until 3.10 pm
                else if (MarketUtils.IsMinutesAfter3Between(0, 10) && !isEODMinProfitSquareOffLimitOrderUpdated)
                {
                    strategy = string.Format("{0}: MinProfit Squareoff using limit sell order", eventType);
                    ordPriceType = OrderPriceType.LIMIT;
                    isEODMinProfitSquareOffLimitOrderUpdated = true;
                    updateSellOrder = true;
                }
            }

            if (updateSellOrder)
            {
                // bought qty needs square off. there is outstanding sell order, revise the price to try square off 
                if (!string.IsNullOrEmpty(todayOutstandingSellOrderId))
                {
                    Trace(strategy);
                    // cancel existing sell order
                    errCode = CancelEquityOrder(eventType, ref todayOutstandingSellOrderId, orderType, OrderDirection.SELL);

                    if (errCode == BrokerErrorCode.Success)
                    {
                        // place new sell order, update sell order ref
                        var sellPrice = GetSellPrice(todayOutstandingPrice, false, true);
                        errCode = PlaceEquityOrder(exchStr, stockCode, OrderDirection.SELL, ordPriceType, todayOutstandingQty, equityOrderType, sellPrice, out todayOutstandingSellOrderId, out upstoxOrderStatus);
                    }
                }
            }
        }

        public void CancelOpenOrders()
        {
            CancelHoldingSellOrder();

            BrokerErrorCode errCode = BrokerErrorCode.Unknown;

            if (!string.IsNullOrEmpty(todayOutstandingSellOrderId))
            {
                var sqOffOrderType = isOutstandingPositionConverted ? EquityOrderType.DELIVERY : orderType;
                errCode = CancelEquityOrder(string.Format("[EOD Cancel SqOff] OrderType:{0}", sqOffOrderType), ref todayOutstandingSellOrderId, sqOffOrderType, OrderDirection.SELL);
            }
        }

        public void CancelHoldingSellOrder()
        {
            BrokerErrorCode errCode = BrokerErrorCode.Unknown;

            if (!string.IsNullOrEmpty(holdingOrder.OrderId) && holdingOrder.UnexecutedQty > 0)
            {
                errCode = CancelEquityOrder(string.Format("[EOD Cancel HoldingOrder] StartingQty:{0} UnexecutedQty:{1} OrderExecutionStatus:{2} ", holdingOrder.StartingQty, holdingOrder.UnexecutedQty, holdingOrder.Status), ref holdingOrder.OrderId, EquityOrderType.DELIVERY, OrderDirection.SELL);

                if (errCode == BrokerErrorCode.Success)
                {
                    // instead of removing the order make the qty 0
                    holdingOrder.UnexecutedQty = 0;
                    holdingOrder.Status = OrderStatus.CANCELLED;
                }
            }

            UpdatePositionFile();
        }

        public void EODProcess(bool isEODLast = false)
        {
            if (isOutstandingPositionConverted || orderType == EquityOrderType.DELIVERY || isEODLast)
            {
                holdingOutstandingPrice = (todayOutstandingPrice * todayOutstandingQty) + (holdingOutstandingQty * holdingOutstandingPrice);
                holdingOutstandingQty += todayOutstandingQty;

                if (holdingOutstandingQty == 0)
                    holdingOutstandingPrice = 0;
                else
                    holdingOutstandingPrice /= holdingOutstandingQty;

                holdingOutstandingPrice = Math.Round(holdingOutstandingPrice, 2);

                UpdatePositionFile(isEODLast);

                todayOutstandingQty = 0;
                todayOutstandingPrice = 0;
            }
        }

        //// holding is merged with today's outtsanding and an avg price is arrived at. this then is updated as holding into positions file
        //public void ConvertToDeliveryAndUpdatePositionFile(bool isEODLast = false)
        //{
        //    var errCode = BrokerErrorCode.Unknown;
        //    bool isConversionSuccessful = false;

        //    // convert to delivery any open buy position
        //    if (todayOutstandingQty > 0 && doConvertToDeliveryAtEOD && orderType == EquityOrderType.MARGIN)
        //    {
        //        // cancel outstanding order to free up the qty for conversion
        //        if (!string.IsNullOrEmpty(todayOutstandingSellOrderId))
        //            errCode = CancelEquityOrder("[EOD Conversion]", ref todayOutstandingSellOrderId, orderType, OrderDirection.SELL);

        //        // convert to delivery, update holding qty and write to positions file
        //        // may need to seperate out and convert each position seperately. currently all outstanding for the stock is tried to convert in single call
        //        if (string.IsNullOrEmpty(todayOutstandingSellOrderId))
        //            errCode = ConvertPendingMarginPositionsToDelivery(stockCode, todayOutstandingQty, todayOutstandingQty, settlementNumber, OrderDirection.BUY, exchStr);

        //        if (errCode != BrokerErrorCode.Success)
        //        {
        //            // Conversion fails.. log the parameters
        //            Trace(string.Format("Today ConversionToDelivery Failed: {0} {1} {2} {3} {4} {5}", stockCode, todayOutstandingQty, todayOutstandingQty, settlementNumber, OrderDirection.BUY, exchange));
        //        }

        //        if (errCode == BrokerErrorCode.Success)
        //            isConversionSuccessful = true;

        //        // if insufficient limit, then try to squareoff
        //        // bought qty needs square off. there is outstanding sell order, revise the price to try square off 
        //        if (errCode == BrokerErrorCode.InsufficientLimit && doSquareOffIfInsufficientLimitAtEOD)
        //        {
        //            BrokerErrorCode errCode1 = BrokerErrorCode.Success;

        //            // cancel existing sell order
        //            if (!string.IsNullOrEmpty(todayOutstandingSellOrderId))
        //                errCode1 = CancelEquityOrder("[Margin EOD] Insufficient Limit to convert. Try to Squareoff", ref todayOutstandingSellOrderId, orderType, OrderDirection.SELL);

        //            if (errCode1 == BrokerErrorCode.Success)
        //            {
        //                // place new sell order, update sell order ref
        //                var sellPrice = GetSellPrice(todayOutstandingPrice, false, false, true);
        //                errCode1 = PlaceEquityOrder(exchStr, stockCode, OrderDirection.SELL, OrderPriceType.LIMIT, todayOutstandingQty, orderType, sellPrice, out todayOutstandingSellOrderId, out upstoxOrderStatus);
        //            }
        //        }
        //    }

        //    if (isConversionSuccessful || orderType == EquityOrderType.DELIVERY || isEODLast)
        //    {
        //        holdingOutstandingPrice = (todayOutstandingPrice * todayOutstandingQty) + (holdingOutstandingQty * holdingOutstandingPrice);
        //        holdingOutstandingQty += todayOutstandingQty;
        //        if (holdingOutstandingQty == 0)
        //            holdingOutstandingPrice = 0;
        //        else
        //            holdingOutstandingPrice /= holdingOutstandingQty;
        //        holdingOutstandingPrice = Math.Round(holdingOutstandingPrice, 2);

        //        UpdatePositionFile(isEODLast);

        //        todayOutstandingQty = 0;
        //    }
        //}

        //public BrokerErrorCode ConvertPendingMarginPositionsToDelivery(string stockCode,
        //    int openQty,
        //    int toConvertQty,
        //    string settlementRef,
        //    OrderDirection ordDirection,
        //    string exchange)
        //{
        //    var errCode = myUpstoxWrapper.ConvertToDeliveryFromMarginOpenPositions(stockCode, openQty, toConvertQty, settlementNumber, ordDirection, exchange);
        //    Trace(string.Format(deliveryTraceFormat, errCode, stockCode, toConvertQty));

        //    return errCode;
        //}

        public void PauseBetweenTradeBookCheck()
        {
            Thread.Sleep(1000 * 15);
        }
    }
}
