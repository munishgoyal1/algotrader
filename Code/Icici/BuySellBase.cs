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
    public class HoldingOrder
    {
        public OrderPositionTypeEnum Type;
        public string SettlementNumber;
        public int Qty;
        public string OrderRef;
        public OrderStatus Status;
    }

    public class BuySellBase
    {
        public double buyPriceCap;
        public double goodPrice;
        public double pctExtraMarkdownForAveraging;
        public double buyMarkdownFromLcpDefault;
        public double sellMarkupDefault;
        public double sellMarkupForDelivery;
        public double sellMarkupForMinProfit;
        public double sellMarkupForEODInsufficientLimitSquareOff;
        public double pctSquareOffForMinProfit;
        public bool squareOffAllPositionsAtEOD;
        public double pctMaxLossSquareOffPositions;
        public bool useAvgBuyPriceInsteadOfLastBuyPriceToCalculateBuyPriceForNewOrder;
        public bool doConvertToDeliveryAtEOD;
        public bool doSquareOffIfInsufficientLimitAtEOD;

        public DateTime startTime;
        public DateTime endTime;

        public IBroker broker = null;
        public string stockCode = null;
        public string isinCode = null;
        public int ordQty = 0;
        public int maxTotalOutstandingQtyAllowed = 0;
        public int maxTodayOutstandingQtyAllowed = 0;
        public int maxBuyOrdersAllowedInADay = 0;
        public Exchange exchange;
        public string positionFile;

        public double holdingOutstandingPrice = 0;
        public int holdingOutstandingQty = 0;
        public double todayOutstandingPrice = 0;
        public int todayOutstandingQty = 0;

        public List<HoldingOrder> holdingsOrders = new List<HoldingOrder>();

        public string todayOutstandingBuyOrderRef = ""; // outstanding buy order ref
        public string todayOutstandingSellOrderRef = "";// outstanding sell order ref
        public bool isEODMinProfitSquareOffLimitOrderUpdated = false;
        public bool isEODMinLossSquareOffMarketOrderUpdated = false;
        public string settlementNumber = "";

        public double lastBuyPrice = 0;
        public bool isFirstBuyOrder = true;

        public int todayBuyOrderCount = 0;
        public const string orderTraceFormat = "{4} Order: {5} {0} {1} {2} @ {3} {6} {7}";
        public const string orderCancelTraceFormat = "{0}: {1} {2} {3}: {4}";
        public const string tradeTraceFormat = "{4} Trade: {0} {1} {2} @ {3}";
        public const string deliveryTraceFormat = "Conversion to delivery: {0} {1} qty of {2}";
        public const string positionFileTotalQtyPriceFormat = "{0} {1}\n";
        public const string positionFileLineQtyPriceFormat = "{0} {1} {2} {3} {4}";

        public BrokerErrorCode errCode;

        public virtual void StockBuySell()
        {

        }
        public BuySellBase(TradeParams tradeParams)
        {
            broker = tradeParams.broker;
            stockCode = tradeParams.stockCode;
            isinCode = tradeParams.isinCode;
            ordQty = tradeParams.ordQty;
            maxTotalOutstandingQtyAllowed = tradeParams.maxTotalOutstandingQtyAllowed;
            maxTodayOutstandingQtyAllowed = tradeParams.maxTodayOutstandingQtyAllowed;
            maxBuyOrdersAllowedInADay = tradeParams.maxBuyOrdersAllowedInADay;
            exchange = tradeParams.exchange;
            positionFile = Path.Combine(SystemUtils.GetPositionFilesLocation(), @"PositionFile_" + stockCode + ".txt");

            buyPriceCap = tradeParams.buyPriceCap;
            goodPrice = tradeParams.goodPrice;
            pctExtraMarkdownForAveraging = tradeParams.pctExtraMarkdownForAveraging;
            buyMarkdownFromLcpDefault = tradeParams.buyMarkdownFromLcpDefault;
            sellMarkupDefault = tradeParams.sellMarkupForMargin;
            sellMarkupForDelivery = tradeParams.sellMarkupForDelivery;
            sellMarkupForMinProfit = tradeParams.sellMarkupForMinProfit;
            sellMarkupForEODInsufficientLimitSquareOff = tradeParams.sellMarkupForEODInsufficientLimitSquareOff;
            squareOffAllPositionsAtEOD = tradeParams.squareOffAllPositionsAtEOD;
            pctMaxLossSquareOffPositions = tradeParams.pctMaxLossSquareOffPositionsAtEOD;
            useAvgBuyPriceInsteadOfLastBuyPriceToCalculateBuyPriceForNewOrder = tradeParams.useAvgBuyPriceInsteadOfLastBuyPriceToCalculateBuyPriceForNewOrder;
            doConvertToDeliveryAtEOD = tradeParams.doConvertToDeliveryAtEOD;
            doSquareOffIfInsufficientLimitAtEOD = tradeParams.doSquareOffIfInsufficientLimitAtEOD;

            startTime = tradeParams.startTime;
            endTime = tradeParams.endTime;
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

            // just place squareoff orders for previous days open positions pending for delivery - develop it later

            // Position file always contains the holding qty, holding price and different type of holdings' details (demat/btst, qty, sell order ref) etc
            ReadPositionFile();

            var orders = new Dictionary<string, EquityOrderBookRecord>();
            var trades = new Dictionary<string, EquityTradeBookRecord>();

            // latest (wrt time) trades or orders appear at start of the output
            errCode = broker.GetEquityTradeBookToday(false, stockCode, out trades);
            errCode = broker.GetEquityOrderBookToday(false, true, stockCode, out orders);

            // get all holding qty trades
            var holdingTradesRef = holdingsOrders.Select(h => h.OrderRef);

            // any outstanding qty (buy minus sell trades) today except from holding qty trade
            todayOutstandingQty = trades.Where(t => !holdingTradesRef.Contains(t.Key)).Sum(t => t.Value.Direction == OrderDirection.SELL ? -t.Value.Quantity : t.Value.Quantity);

            var buyTrades = trades.Where(t => t.Value.Direction == OrderDirection.BUY);
            lastBuyPrice = buyTrades.Any() ? buyTrades.First().Value.Price : holdingOutstandingPrice;
            settlementNumber = buyTrades.Any() ? buyTrades.First().Value.SettlementNumber : "";

            var numOutstandingBuyTrades = todayOutstandingQty > 0 ? todayOutstandingQty / ordQty : 0;
            // these are latest trades taken. each buy trade is for single lot and thus for each lot there is a trade
            todayOutstandingPrice = numOutstandingBuyTrades == 0 ? 0 : buyTrades.Take(numOutstandingBuyTrades).Average(t => t.Value.Price);

            ProcessHoldingSellOrderExecution(trades);

            var buyOrders = orders.Values.Where(o => o.Direction == OrderDirection.BUY && o.Status == OrderStatus.ORDERED);
            var sellOrders = orders.Values.Where(o => o.Direction == OrderDirection.SELL && o.Status == OrderStatus.ORDERED && !holdingTradesRef.Contains(o.OrderRefenceNumber)); // pick only today's outstanding related sell orders

            // assumed that there is always at max only single outstanding buy order and a at max single outstanding sell order
            todayOutstandingBuyOrderRef = buyOrders.Any() ? buyOrders.First().OrderRefenceNumber : "";
            todayOutstandingSellOrderRef = sellOrders.Any() ? sellOrders.First().OrderRefenceNumber : "";

            var sellOrder = sellOrders.Any() ? sellOrders.First() : null;

            isFirstBuyOrder = string.IsNullOrEmpty(todayOutstandingBuyOrderRef) && !trades.Where(t => t.Value.Direction == OrderDirection.BUY).Any();

            // if sqoff sell order for holdings is needed then place it 
            //assumption is: if there is a holding pending from day before then it would have been converted to delivery
            if (holdingOutstandingQty > 0)
            {
                // get demat and btst listings
                List<EquityDematHoldingRecord> dematHoldings;
                errCode = broker.GetDematAllocation(stockCode, out dematHoldings);
                List<EquityBTSTTradeBookRecord> btstHoldings;
                errCode = broker.GetBTSTListings(stockCode, out btstHoldings);
                List<EquityPendingPositionForDelivery> pendingPositions;
                errCode = broker.GetOpenPositionsPendingForDelivery(stockCode, out pendingPositions);

                // place sq off sell order, update sell order ref
                var sellPrice = GetSellPrice(holdingOutstandingPrice, true, false);
                string sellOrderRef = "";
                if (dematHoldings.Any())
                {
                    var dematHolding = dematHoldings.First();
                    if (dematHolding.AvailableQuantity > 0)
                    {
                        errCode = PlaceEquityOrder(stockCode, OrderPositionTypeEnum.Demat, dematHolding.AvailableQuantity, dematHolding.AvailableQuantity, sellPrice.ToString(), OrderPriceType.LIMIT, OrderDirection.SELL, EquityOrderType.DELIVERY, exchange, "", out sellOrderRef);
                        if (errCode == BrokerErrorCode.Success)
                        {
                            holdingsOrders.Add(new HoldingOrder { Type = OrderPositionTypeEnum.Demat, OrderRef = sellOrderRef, Qty = dematHolding.AvailableQuantity, SettlementNumber = "" });
                        }
                    }
                }
                if (btstHoldings.Any())
                {
                    foreach (var btstHolding in btstHoldings)
                    {
                        if (btstHolding.AvailableQuantity > 0)
                        {
                            errCode = PlaceEquityOrder(stockCode, OrderPositionTypeEnum.Btst, btstHolding.AvailableQuantity, btstHolding.AvailableQuantity, sellPrice.ToString(), OrderPriceType.LIMIT, OrderDirection.SELL, EquityOrderType.DELIVERY, exchange, btstHolding.SettlementNumber, out sellOrderRef);
                            if (errCode == BrokerErrorCode.Success)
                            {
                                holdingsOrders.Add(new HoldingOrder { Type = OrderPositionTypeEnum.Btst, OrderRef = sellOrderRef, Qty = btstHolding.AvailableQuantity, SettlementNumber = btstHolding.SettlementNumber });
                            }
                        }
                    }
                }
                if (pendingPositions.Any())
                {
                    foreach (var pendingPosition in pendingPositions)
                    {
                        if (pendingPosition.AvailableQuantity > 0)
                        {
                            errCode = PlaceEquityOrder(stockCode, OrderPositionTypeEnum.OpenPendingDelivery, pendingPosition.AvailableQuantity, pendingPosition.AvailableQuantity, sellPrice.ToString(), OrderPriceType.LIMIT, OrderDirection.SELL, EquityOrderType.DELIVERY, exchange, pendingPosition.SettlementNumber, out sellOrderRef);
                            if (errCode == BrokerErrorCode.Success)
                            {
                                holdingsOrders.Add(new HoldingOrder { Type = OrderPositionTypeEnum.OpenPendingDelivery, OrderRef = sellOrderRef, Qty = pendingPosition.AvailableQuantity, SettlementNumber = pendingPosition.SettlementNumber });
                            }
                        }
                    }
                }
                UpdatePositionFile();
            }

            // For AverageTheBuyThenSell algo - place sell order for outstanding qty if not already present
            if (algoType == AlgoType.AverageTheBuyThenSell)
            {
                // check if the outstanding sell order has matching qty or not
                if (sellOrder != null && !string.IsNullOrEmpty(todayOutstandingSellOrderRef) && sellOrder.Quantity < todayOutstandingQty)
                {
                    // Cancel existing sell order 
                    errCode = CancelEquityOrder("[Init Update Sell Qty]", ref todayOutstandingSellOrderRef, EquityOrderType.MARGIN, OrderDirection.SELL);
                }

                if (string.IsNullOrEmpty(todayOutstandingSellOrderRef) && todayOutstandingQty > 0)
                {
                    var sellPrice = GetSellPrice(todayOutstandingPrice, false, false);
                    errCode = PlaceEquityOrder(stockCode, OrderPositionTypeEnum.Margin, todayOutstandingQty, todayOutstandingQty, sellPrice.ToString(), OrderPriceType.LIMIT, OrderDirection.SELL, EquityOrderType.MARGIN, exchange, "", out todayOutstandingSellOrderRef);
                }
            }

            todayBuyOrderCount = buyOrders.Count();
        }

        protected void ProcessHoldingSellOrderExecution(Dictionary<string, EquityTradeBookRecord> trades)
        {
            var holdingTradesRef = holdingsOrders.Select(h => h.OrderRef);

            if (!holdingTradesRef.Any(htr => trades.ContainsKey(htr)))
                return;

            var holdingsOrdersToRemove = new List<HoldingOrder>();

            foreach (var holdingOrder in holdingsOrders.Where(ho => ho.Qty > 0 && !string.IsNullOrEmpty(ho.OrderRef)))
            {
                var orderRef = holdingOrder.OrderRef;

                if (trades.ContainsKey(orderRef))
                {
                    var trade = trades[orderRef];
                    holdingOutstandingQty -= trade.NewQuantity;
                    holdingOrder.Qty -= trade.NewQuantity;

                    holdingOutstandingQty = holdingOutstandingQty < 0 ? 0 : holdingOutstandingQty;
                    holdingOrder.Qty = holdingOrder.Qty < 0 ? 0 : holdingOrder.Qty;

                    holdingOrder.Status = OrderStatus.PARTEXEC;

                    if (holdingOrder.Qty == 0)
                    {
                        // instead of removing the order mark the status
                        holdingOrder.Status = OrderStatus.EXECUTED;
                        //holdingsOrdersToRemove.Add(holdingOrder);
                    }
                }
            }

            holdingsOrders.RemoveAll(h => holdingsOrdersToRemove.Contains(h));

            if (holdingOutstandingQty == 0)
                holdingOutstandingPrice = 0;

            UpdatePositionFile();
        }

        public BrokerErrorCode CancelEquityOrder(string source, ref string orderRef, EquityOrderType productType, OrderDirection side)
        {
            var errCode = CancelEquityOrder(source, orderRef, productType, side);
            if (errCode == BrokerErrorCode.Success)
                orderRef = "";
            return errCode;
        }

        public BrokerErrorCode CancelEquityOrder(string source, string orderRef, EquityOrderType productType, OrderDirection side)
        {
            var errCode = broker.CancelEquityOrder(orderRef, productType);
            Trace(string.Format(orderCancelTraceFormat, source, stockCode, side + " order cancel", ", OrderRef = " + orderRef, errCode));
            return errCode;
        }

        public double GetSellPrice(double price, bool isForDeliveryQty, bool isForMinProfitSquareOff, bool isInsufficientLimitEODSquareOff = false)
        {
            var factor = sellMarkupDefault;  // default

            if (isForDeliveryQty)
                factor = sellMarkupForDelivery;

            if (isForMinProfitSquareOff)
                factor = sellMarkupForMinProfit;

            if (isInsufficientLimitEODSquareOff)
                factor = sellMarkupForEODInsufficientLimitSquareOff;

            return Math.Round(factor * price, 1);
        }

        public double GetBuySquareOffPrice(double price)
        {
            var factor = 1 - pctSquareOffForMinProfit;

            return Math.Round(factor * price, 1);
        }

        // TODO: refactor.. not being used currently
        public double GetBuyPrice(double ltp, bool isTodayFirstOrder, bool doesHoldingPositionExist)
        {
            var factor = 1 - buyMarkdownFromLcpDefault;  // default



            //if (!isTodayFirstOrder)
            //    factor = sellMarkupForDelivery;

            //if (doesHoldingPositionExist)
            //    factor = sellMarkupForMinProfit;

            return Math.Round(factor * ltp, 1);
        }

        public BrokerErrorCode GetLTP(out double ltp)
        {
            EquitySymbolQuote[] quote;
            BrokerErrorCode errCode = BrokerErrorCode.Unknown;
            int retryCount = 0;
            ltp = 0.0;

            while (errCode != BrokerErrorCode.Success && retryCount++ < 3)
                try
                {
                    errCode = broker.GetEquityQuote(stockCode, out quote);
                    var idx = exchange == Exchange.NSE ? 0 : 1;
                    ltp = quote[idx].LastTradePriceDouble;
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

        public BrokerErrorCode PlaceEquityOrder(string stockCode,
            OrderPositionTypeEnum holdingType,
            int availableQty,
            int quantity,
            string price,
            OrderPriceType orderPriceType,
            OrderDirection orderDirection,
            EquityOrderType orderType,
            Exchange exchange,
            string settlementNumber,
            out string orderRef)
        {
            BrokerErrorCode errCode = BrokerErrorCode.Unknown;
            orderRef = "";

            // Only OrderPositionTypeEnum.Margin is Buy/Sell, rest all others are only sell orders
            if (holdingType == OrderPositionTypeEnum.Btst)  //  only a sell order
                errCode = broker.PlaceEquityDeliveryBTSTOrder(stockCode, quantity, price, orderPriceType, orderDirection, orderType, exchange, settlementNumber, out orderRef);
            else if (holdingType == OrderPositionTypeEnum.Demat || holdingType == OrderPositionTypeEnum.Margin)
                errCode = broker.PlaceEquityMarginDeliveryFBSOrder(stockCode, quantity, price, orderPriceType, orderDirection, orderType, exchange, out orderRef);
            else if (holdingType == OrderPositionTypeEnum.OpenPendingDelivery) // only square off sell order
                errCode = broker.PlaceEquityMarginSquareOffOrder(stockCode, availableQty, quantity, price, orderPriceType, orderDirection, orderType, settlementNumber, exchange, out orderRef);

            var orderTypeStr = orderType == EquityOrderType.DELIVERY ? ("CASH " + holdingType) : "MARGIN";
            Trace(string.Format(orderTraceFormat, stockCode, orderDirection, quantity, price, orderTypeStr, errCode, orderPriceType, settlementNumber));

            if (errCode == BrokerErrorCode.Success && orderDirection == OrderDirection.BUY)
            {
                todayBuyOrderCount++;
                if (todayBuyOrderCount >= maxBuyOrdersAllowedInADay)
                    Trace(string.Format("Buy order count reached is: {0}. Max buy orders allowed: {1}", todayBuyOrderCount, maxBuyOrdersAllowedInADay));
            }

            return errCode;
        }

        public BrokerErrorCode ConvertPendingMarginPositionsToDelivery(string stockCode,
            int openQty,
            int toConvertQty,
            string settlementRef,
            OrderDirection ordDirection,
            Exchange exchange)
        {
            var errCode = broker.ConvertToDeliveryFromMarginOpenPositions(stockCode, openQty, toConvertQty, settlementNumber, ordDirection, exchange);
            Trace(string.Format(deliveryTraceFormat, errCode, stockCode, toConvertQty));

            return errCode;
        }

        public BrokerErrorCode ConvertToDeliveryFromPendingForDelivery(string stockCode,
            int openQty,
            int toConvertQty,
            string settlementRef,
            Exchange exchange)
        {
            var errCode = broker.ConvertToDeliveryFromPendingForDelivery(stockCode, openQty, toConvertQty, settlementNumber, exchange);
            Trace(string.Format(deliveryTraceFormat, errCode, stockCode, toConvertQty));

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
                    }
                    else
                    {
                        var holdingOrder = new HoldingOrder();
                        holdingOrder.Type = (OrderPositionTypeEnum)Enum.Parse(typeof(OrderPositionTypeEnum), split[0].Trim());
                        holdingOrder.SettlementNumber = split[1].Trim();
                        holdingOrder.Qty = int.Parse(split[2].Trim());
                        holdingOrder.OrderRef = split[3].Trim();
                        holdingOrder.Status = (OrderStatus)Enum.Parse(typeof(OrderStatus), split[4].Trim());
                        holdingsOrders.Add(holdingOrder);
                    }
                }
            }
        }

        public void UpdatePositionFile(bool isEODUpdate = false)
        {
            var lines = new List<string>();
            var positionTotalStr = string.Format(positionFileTotalQtyPriceFormat, holdingOutstandingQty, holdingOutstandingPrice);
            lines.Add(positionTotalStr);
            if (!isEODUpdate)
            {
                foreach (var holdingOrder in holdingsOrders)
                {
                    var positionLineStr = string.Format(positionFileLineQtyPriceFormat, holdingOrder.Type, holdingOrder.SettlementNumber, holdingOrder.Qty, holdingOrder.OrderRef, holdingOrder.Status);
                    lines.Add(positionLineStr);
                }
            }
            File.WriteAllLines(positionFile, lines);

            if (isEODUpdate)
                Trace(string.Format("{2}PositionFile updated as {0} {1}", holdingOutstandingQty, holdingOutstandingPrice, isEODUpdate ? "EOD " : ""));
        }


        // Try min profit squareoff first between 3 - 3.10 time.
        // From 3.10 to 3.15 time if squareoff of all positions is set to true and the ltp diff meets threshold for max loss pct, then do a market order squareoff 
        public void TrySquareOffNearEOD(AlgoType algoType)
        {
            // if after 3 pm, then try to square off in at least no profit no loss if possible. cancel the outstanding buys anyway
            if (MarketUtils.IsTimeAfter310())
            {
                var ordPriceType = OrderPriceType.LIMIT;
                var doUpdateOrders = false;

                // 3.20 - 3.25 pm time. market order type for forced square off given pct loss is within acceptable range
                // do it before 3.15, otherwise broker will try to squareoff on its own anytime between 3.15 - 3.30
                if (MarketUtils.IsTimeAfter320() && !MarketUtils.IsTimeAfter325() && squareOffAllPositionsAtEOD && !isEODMinLossSquareOffMarketOrderUpdated)
                {
                    double ltp;
                    var errCode = GetLTP(out ltp);

                    if (errCode != BrokerErrorCode.Success)
                        return;

                    ordPriceType = OrderPriceType.MARKET;

                    var diff = (ltp - todayOutstandingPrice) / ltp;

                    Trace(string.Format("[Margin EOD]: diff {0} ltp {1} outstandingprice {2} pctMaxLossSquareOffPositions {3} goodPrice {4} ", diff, ltp, todayOutstandingPrice, pctMaxLossSquareOffPositions, goodPrice));

                    if ((Math.Abs(diff) < pctMaxLossSquareOffPositions || diff > 0) && todayOutstandingPrice > goodPrice)
                    {
                        Trace(string.Format("[Margin EOD]: max loss {0}% is less than {1}% and avg outstanding price {2} is greater than good price of {3}. LTP is {4}. Place squareoff @ MARKET.", diff, pctMaxLossSquareOffPositions, todayOutstandingPrice, goodPrice, ltp));
                        doUpdateOrders = true;
                        isEODMinLossSquareOffMarketOrderUpdated = true;
                    }
                }

                // 3.10 - 3.20 pm time. try simple limit order with min profit price. watch until 3.10 pm
                else if (!isEODMinProfitSquareOffLimitOrderUpdated)
                {
                    Trace(string.Format("[Margin EOD]: MinProfit Squareoff and cancel outstanding buy orders"));
                    ordPriceType = OrderPriceType.LIMIT;
                    isEODMinProfitSquareOffLimitOrderUpdated = true;
                    doUpdateOrders = true;
                }

                if (doUpdateOrders)
                {
                    if (algoType == AlgoType.AverageTheBuyThenSell)
                    {
                        // just cancel the outstanding buy order
                        if (!string.IsNullOrEmpty(todayOutstandingBuyOrderRef))
                        {
                            // cancel existing buy order
                            errCode = CancelEquityOrder("[Margin EOD]", ref todayOutstandingBuyOrderRef, EquityOrderType.MARGIN, OrderDirection.BUY);
                        }

                        // bought qty needs square off. there is outstanding sell order, revise the price to try square off 
                        if (!string.IsNullOrEmpty(todayOutstandingSellOrderRef))
                        {
                            // cancel existing sell order
                            errCode = CancelEquityOrder("[Margin EOD]", ref todayOutstandingSellOrderRef, EquityOrderType.MARGIN, OrderDirection.SELL);

                            if (errCode == BrokerErrorCode.Success)
                            {
                                // place new sell order, update sell order ref
                                var sellPrice = GetSellPrice(todayOutstandingPrice, false, true);
                                errCode = PlaceEquityOrder(stockCode, OrderPositionTypeEnum.Margin, todayOutstandingQty, todayOutstandingQty, sellPrice.ToString(), ordPriceType, OrderDirection.SELL, EquityOrderType.MARGIN, exchange, "", out todayOutstandingSellOrderRef);
                            }
                        }
                    }
                }
            }
        }

        public void CancelHoldingSellOrders()
        {
            if (!holdingsOrders.Any())
                return;

            BrokerErrorCode errCode = BrokerErrorCode.Unknown;
            var holdingsOrdersToRemove = new List<HoldingOrder>();
            foreach (var holdingOrder in holdingsOrders)
            {
                if (!string.IsNullOrEmpty(holdingOrder.OrderRef))
                {
                    errCode = CancelEquityOrder(string.Format("[Holding EOD] {0} {1}", holdingOrder.Type, holdingOrder.SettlementNumber), ref holdingOrder.OrderRef, EquityOrderType.DELIVERY, OrderDirection.SELL);

                    if (errCode == BrokerErrorCode.Success)
                    {
                        // instead of removing the order make the qty 0
                        holdingOrder.Qty = 0;
                        holdingOrder.Status = OrderStatus.CANCELLED;
                        //holdingsOrdersToRemove.Add(holdingOrder);
                    }
                }
            }

            holdingsOrders.RemoveAll(h => holdingsOrdersToRemove.Contains(h));
            UpdatePositionFile();
        }

        public void ConvertToDeliveryPreviousDaysExpiringOpenPositions()
        {

            if (!MarketUtils.IsTimeAfter2XMin(50))
                return;

            BrokerErrorCode errCode = BrokerErrorCode.Unknown;

            // Get holding order matching this. check expiry date. if today then cancel holding order and convert it
            List<EquityPendingPositionForDelivery> pendingPositions;
            errCode = broker.GetOpenPositionsPendingForDelivery(stockCode, out pendingPositions);

            var positionsExpiringToday = pendingPositions.Where(p => p.ExpiryDate <= DateTime.Today.Date);

            var holdingsOrdersToRemove = new List<HoldingOrder>();

            foreach (var position in positionsExpiringToday)
            {
                var holdingOrder = holdingsOrders.Where(h => h.Type == OrderPositionTypeEnum.OpenPendingDelivery && h.SettlementNumber == position.SettlementNumber && !string.IsNullOrEmpty(h.OrderRef) && h.Qty == position.BlockedQuantity && h.Qty > 0).FirstOrDefault();
                var qtyToConvert = holdingOrder.Qty;

                // free up the blocked qty to go ahead with conversion
                errCode = CancelEquityOrder(string.Format("[Holding Conversion EOD] {0} {1}", holdingOrder.Type, holdingOrder.SettlementNumber), ref holdingOrder.OrderRef, EquityOrderType.DELIVERY, OrderDirection.SELL);
                if (errCode == BrokerErrorCode.Success)
                {
                    // instead of removing the order mark the status as Cancelled and set Qty to 0
                    holdingOrder.Qty = 0;
                    holdingOrder.Status = OrderStatus.CANCELLED;
                    //holdingsOrdersToRemove.Add(holdingOrder);
                }

                int retryCount = 0;
                errCode = BrokerErrorCode.Unknown;
                while (errCode != BrokerErrorCode.Success && retryCount++ < 3)
                {
                    // Just to ensure the qty is freed up and some time before retry
                    Thread.Sleep(5000);
                    // convert to delivery
                    errCode = ConvertToDeliveryFromPendingForDelivery(stockCode, qtyToConvert, qtyToConvert, position.SettlementNumber, position.Exchange);
                    if (errCode != BrokerErrorCode.Success)
                    {
                        // Conversion fails.. log the parameters
                        Trace(string.Format("Previous ConversionToDelivery Failed: {5} {0} {1} qty expiring on {2} {3} {4}", stockCode, qtyToConvert, position.ExpiryDate.Date, position.SettlementNumber, position.Exchange, errCode));
                    }
                }
            }

            if (holdingsOrdersToRemove.Any())
            {
                holdingsOrders.RemoveAll(h => holdingsOrdersToRemove.Contains(h));
                UpdatePositionFile();
            }
        }

        // holding is merged with today's outtsanding and an avg price is arrived at. this then is updated as holding into positions file
        public void ConvertToDeliveryAndUpdatePositionFile(bool isEODLast = false)
        {
            var errCode = BrokerErrorCode.Unknown;
            bool isConversionSuccessful = false;

            // convert to delivery any open buy position
            if (todayOutstandingQty > 0 && doConvertToDeliveryAtEOD)
            {
                // cancel outstanding order to free up the qty for conversion
                if (!string.IsNullOrEmpty(todayOutstandingSellOrderRef))
                    errCode = CancelEquityOrder("[Margin Conversion EOD]", ref todayOutstandingSellOrderRef, EquityOrderType.MARGIN, OrderDirection.SELL);

                // convert to delivery, update holding qty and write to positions file
                // may need to seperate out and convert each position seperately. currently all outstanding for the stock is tried to convert in single call
                if (string.IsNullOrEmpty(todayOutstandingSellOrderRef))
                    errCode = ConvertPendingMarginPositionsToDelivery(stockCode, todayOutstandingQty, todayOutstandingQty, settlementNumber, OrderDirection.BUY, exchange);

                if (errCode != BrokerErrorCode.Success)
                {
                    // Conversion fails.. log the parameters
                    Trace(string.Format("Today ConversionToDelivery Failed: {0} {1} {2} {3} {4} {5}", stockCode, todayOutstandingQty, todayOutstandingQty, settlementNumber, OrderDirection.BUY, exchange));
                }

                if (errCode == BrokerErrorCode.Success)
                    isConversionSuccessful = true;

                // if insufficient limit, then try to squareoff
                // bought qty needs square off. there is outstanding sell order, revise the price to try square off 
                if (errCode == BrokerErrorCode.InsufficientLimit && doSquareOffIfInsufficientLimitAtEOD)
                {
                    BrokerErrorCode errCode1 = BrokerErrorCode.Success;

                    // cancel existing sell order
                    if (!string.IsNullOrEmpty(todayOutstandingSellOrderRef))
                        errCode1 = CancelEquityOrder("[Margin EOD] Insufficient Limit to convert. Try to Squareoff", ref todayOutstandingSellOrderRef, EquityOrderType.MARGIN, OrderDirection.SELL);

                    if (errCode1 == BrokerErrorCode.Success)
                    {
                        // place new sell order, update sell order ref
                        var sellPrice = GetSellPrice(todayOutstandingPrice, false, false, true);
                        errCode1 = PlaceEquityOrder(stockCode, OrderPositionTypeEnum.Margin, todayOutstandingQty, todayOutstandingQty, sellPrice.ToString(), OrderPriceType.LIMIT, OrderDirection.SELL, EquityOrderType.MARGIN, exchange, "", out todayOutstandingSellOrderRef);
                    }
                }
            }

            if (isConversionSuccessful || isEODLast)
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
            }
        }

        public void PauseBetweenTradeBookCheck()
        {
            Thread.Sleep(1000 * 60);
        }
    }
}
