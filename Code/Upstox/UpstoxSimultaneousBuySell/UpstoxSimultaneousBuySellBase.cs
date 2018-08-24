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
    public class SimultaneousBuySellBase
    {
        public double buyPriceCap;
        public double goodPrice;
        public double pctLtpOfLastBuyPriceForAveraging;
        public double buyMarkdownFromLtpDefault;
        public double sellMarkupDefault;
        public double sellMarkupForDelivery;
        public double sellMarkupForMinProfit;
        public double pctSquareOffForMinProfit;
        public bool squareOffAllPositionsAtEOD;
        public double pctMaxLossSquareOffPositions;
        public bool useAvgBuyPriceInsteadOfLastBuyPriceToCalculateBuyPriceForNewOrder;

        public DateTime startTime;
        public DateTime endTime;

        public IBroker broker = null;
        public string stockCode = null;
        public string isinCode = null;
        public int ordQty = 0;
        public int maxAllowedOutstandingQty = 0;
        public int maxBuyOrders = 0;
        public Exchange exchange;
        public string positionFile;

        public double holdingOutstandingPrice = 0;
        public int holdingOutstandingQty = 0;
        public double todayOutstandingPrice = 0;
        public int todayOutstandingQty = 0;

        public string holdingSellOrderRef = "";
        public string todayOutstandingBuyOrderRef = ""; // outstanding buy order ref
        public string todayOutstandingSellOrderRef = "";// outstanding sell order ref
        public bool isEODMinProfitSquareOffLimitOrderUpdated = false;
        public bool isEODMinLossSquareOffMarketOrderUpdated = false;
        public string settlementNumber = "";

        public double lastBuyPrice = 0;
        public bool isFirstBuyOrder = true;

        public int buyOrderCount = 0;
        public const string orderTraceFormat = "{4} Order: {5} {0} {1} {2} @ {3} {6}";
        public const string orderCancelTraceFormat = "Cancel Order {0}: {1} {2} {3}: {4}";
        public const string tradeTraceFormat = "{4} Trade: {0} {1} {2} @ {3}";
        public const string deliveryTraceFormat = "Conversion to delivery: {0} {1} qty of {2}";
        public const string positionFileFormat = "{0} {1} {2}";

        public BrokerErrorCode errCode;

        public virtual void StockBuySell()
        {

        }
        public SimultaneousBuySellBase(UpstoxTradeParams tradeParams)
        {
            broker = tradeParams.upstox;
            stockCode = tradeParams.stockCode;
            isinCode = tradeParams.isinCode;
            ordQty = tradeParams.ordQty;
            maxAllowedOutstandingQty = tradeParams.maxTotalOutstandingQtyAllowed;
            maxBuyOrders = tradeParams.maxBuyOrdersAllowedInADay;
            exchange = tradeParams.exchange;
            positionFile = @"PositionFile_" + stockCode + ".txt";

            buyPriceCap = tradeParams.buyPriceCap;
            goodPrice = tradeParams.buyPriceCap;
            pctLtpOfLastBuyPriceForAveraging = tradeParams.pctExtraMarkdownForAveraging;
            buyMarkdownFromLtpDefault = tradeParams.buyMarkdownFromLcpDefault;
            sellMarkupDefault = tradeParams.sellMarkupForMargin;
            sellMarkupForDelivery = tradeParams.sellMarkupForDelivery;
            sellMarkupForMinProfit = tradeParams.sellMarkupForMinProfit;
            squareOffAllPositionsAtEOD = tradeParams.squareOffAllPositionsAtEOD;
            pctMaxLossSquareOffPositions = tradeParams.pctMaxLossSquareOffPositionsAtEOD;
            useAvgBuyPriceInsteadOfLastBuyPriceToCalculateBuyPriceForNewOrder = tradeParams.useAvgBuyPriceInsteadOfLastBuyPriceToCalculateBuyPriceForNewOrder;

            startTime = tradeParams.startTime;
            endTime = tradeParams.endTime;
        }

        public bool IsOrderTimeWithinRange()
        {
            // start and end timings
            return DateTime.Now >= startTime && DateTime.Now <= endTime;
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

            // Position file always contains the holding qty, holding price and holding sell order ref
            ReadPositionFile();

            var orders = new Dictionary<string, EquityOrderBookRecord>();
            var trades = new Dictionary<string, EquityTradeBookRecord>();

            // latest (wrt time) trades or orders appear at start of the output
            errCode = broker.GetEquityTradeBookToday(false, stockCode, out trades);
            errCode = broker.GetEquityOrderBookToday(false, true, stockCode, out orders);

            // any outstanding qty (buy minus sell trades) today except from holding qty trade
            todayOutstandingQty = trades.Where(t => t.Key != holdingSellOrderRef).Sum(t => t.Value.Direction == OrderDirection.SELL ? -t.Value.Quantity : t.Value.Quantity);

            var buyTrades = trades.Where(t => t.Value.Direction == OrderDirection.BUY);
            lastBuyPrice = buyTrades.Any() ? buyTrades.First().Value.Price : 0;

            var numOutstandingBuyTrades = todayOutstandingQty > 0 ? todayOutstandingQty / ordQty : 0;
            // these are latest trades taken. each buy trade is for single lot and thus for each lot there is a trade
            todayOutstandingPrice = numOutstandingBuyTrades == 0 ? 0 : buyTrades.Take(numOutstandingBuyTrades).Average(t => t.Value.Price);

            if (trades.ContainsKey(holdingSellOrderRef))
                ProcessHoldingSellOrderExecution();

            var buyOrders = orders.Values.Where(o => o.Direction == OrderDirection.BUY && o.Status == OrderStatus.ORDERED);
            var sellOrders = orders.Values.Where(o => o.Direction == OrderDirection.SELL && o.Status == OrderStatus.ORDERED && o.OrderRefenceNumber != holdingSellOrderRef); // pick only today's outstanding related sell orders

            // assumed that there is always at max only single outstanding buy order and a at max single outstanding sell order
            todayOutstandingBuyOrderRef = buyOrders.Any() ? buyOrders.First().OrderRefenceNumber : "";
            todayOutstandingSellOrderRef = sellOrders.Any() ? sellOrders.First().OrderRefenceNumber : "";

            isFirstBuyOrder = string.IsNullOrEmpty(todayOutstandingBuyOrderRef) && !trades.Where(t => t.Value.Direction == OrderDirection.BUY).Any();

            // if sqoff sell order for holdings is needed then place it 
            //assumption is: if there is a holding pending from day before then it would have been converted to delivery
            if (holdingOutstandingQty > 0 && string.IsNullOrEmpty(holdingSellOrderRef))
            {
                // place sq off sell order, update sell order ref
                var sellPrice = GetSellPrice(holdingOutstandingPrice, true);
                errCode = PlaceEquityOrder(stockCode, holdingOutstandingQty, sellPrice.ToString(), OrderPriceType.LIMIT, OrderDirection.SELL, EquityOrderType.DELIVERY, exchange, out holdingSellOrderRef);
                if (errCode == BrokerErrorCode.Success)
                {
                    UpdatePositionFile(holdingOutstandingQty, holdingOutstandingPrice, holdingSellOrderRef);
                    Trace(string.Format(orderTraceFormat, stockCode, "cash holding squareoff sell", holdingOutstandingQty, sellPrice, "CASH", errCode, OrderPriceType.LIMIT));
                }
            }

            // For AverageTheBuyThenSell algo - place sell order for outstanding qty if not already present
            if(algoType == AlgoType.AverageTheBuyThenSell)
            {
                if (string.IsNullOrEmpty(todayOutstandingSellOrderRef) && todayOutstandingQty > 0)
                {
                    var sellPrice = GetSellPrice(todayOutstandingPrice, false);
                    errCode = PlaceEquityOrder(stockCode, todayOutstandingQty, sellPrice.ToString(), OrderPriceType.LIMIT, OrderDirection.SELL, EquityOrderType.MARGIN, exchange, out todayOutstandingSellOrderRef);
                }
            }

            buyOrderCount = buyOrders.Count();
        }

        protected void ProcessHoldingSellOrderExecution()
        {
            holdingOutstandingQty = 0;
            holdingOutstandingPrice = 0;
            holdingSellOrderRef = "";

            UpdatePositionFile(holdingOutstandingQty, holdingOutstandingPrice, holdingSellOrderRef);
        }

        public void ReadPositionFile()
        {
            var positionsStr = File.ReadAllText(positionFile);
            if (!string.IsNullOrEmpty(positionsStr))
            {
                var split = positionsStr.Split(' ');
                holdingOutstandingQty = int.Parse(split[0]);
                holdingOutstandingPrice = double.Parse(split[1]);
                if (split.Length > 2)
                    holdingSellOrderRef = split[2];
            }
        }

        public BrokerErrorCode CancelEquityOrder(string source, string orderRef, OrderDirection side)
        {
            var errCode = broker.CancelEquityOrder(orderRef, EquityOrderType.MARGIN);
            Trace(string.Format(orderCancelTraceFormat, source, stockCode, side + " order cancel", ", OrderRef = " + orderRef, errCode));
            return errCode;
        }

        public BrokerErrorCode CancelEquityOrder(string source, ref string orderRef, OrderDirection side)
        {
            var errCode = CancelEquityOrder(source, orderRef, side);
            if (errCode == BrokerErrorCode.Success)
                orderRef = "";
            return errCode;
        }

        public double GetSellPrice(double price, bool isForDeliveryQty, bool isForMinProfitSquareOff = false)
        {
            var factor = sellMarkupDefault;  // default

            if (isForDeliveryQty)
                factor = sellMarkupForDelivery;

            if (isForMinProfitSquareOff)
                factor = sellMarkupForMinProfit;

            return Math.Round(factor * price, 1);
        }

        public double GetBuySquareOffPrice(double price)
        {
            var factor = 1 - pctSquareOffForMinProfit;

            return Math.Round(factor * price, 1);
        }

        public double GetBuyPrice()
        {
            EquitySymbolQuote[] quote;
            broker.GetEquityQuote(stockCode, out quote);

            var idx = exchange == Exchange.NSE ? 0 : 1;

            //var ltp = isFirstBuyOrder && !MarketUtils.IsTimeAfter920() ? quote[idx].PreviousClosePriceDouble : quote[idx].LastTradePriceDouble;
            var ltp = quote[idx].LastTradePriceDouble;
            return Math.Round(buyMarkdownFromLtpDefault * ltp, 1);
        }

        public double GetLTP()
        {
            EquitySymbolQuote[] quote;
            broker.GetEquityQuote(stockCode, out quote);

            var idx = exchange == Exchange.NSE ? 0 : 1;

            return quote[idx].LastTradePriceDouble;
        }

        public void Trace(string message)
        {
            message = GetType().Name + " " + stockCode + " " + message;
            Console.WriteLine(DateTime.Now.ToString() + " " + message);
            FileTracing.TraceOut(message);
        }

        public BrokerErrorCode PlaceEquityOrder(string stockCode,
            int quantity,
            string price,
            OrderPriceType orderPriceType,
            OrderDirection orderDirection,
            EquityOrderType orderType,
            Exchange exchange,
            out string orderRef)
        {
            var errCode = broker.PlaceEquityMarginDeliveryFBSOrder(stockCode, quantity, price, orderPriceType, orderDirection, orderType, exchange, out orderRef);
            
            Trace(string.Format(orderTraceFormat, stockCode, orderDirection, quantity, price, orderType == EquityOrderType.DELIVERY ? "CASH" : "MARGIN", errCode, orderPriceType));

            if (orderDirection == OrderDirection.BUY)
            {
                buyOrderCount++;
                if (buyOrderCount >= maxBuyOrders)
                    Trace(string.Format("Buy order count reached is: {0}. Max buy orders allowed: {1}", buyOrderCount, maxBuyOrders));
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

        public void UpdatePositionFile(int qty, double price, string orderRef)
        {
            var positionStr = string.Format(positionFileFormat, qty, price, orderRef);
            File.WriteAllText(positionFile, positionStr);
            Trace(string.Format("PositionFile updated as {0} {1} {2}", qty, price, orderRef));
        }


        // Try min profit squareoff first between 3 - 3.10 time.
        // From 3.10 to 3.15 time if squareoff of all positions is set to true and the ltp diff meets threshold for max loss pct, then do a market order squareoff 
        public void TrySquareOffNearEOD(AlgoType algoType)
        {
            // if after 3 pm, then try to square off in at least no profit no loss if possible. either buy or sell is outstanding
            if (MarketUtils.IsTimeAfter3())
            {
                var ordPriceType = OrderPriceType.LIMIT;
                var doUpdateOrders = false;

                // 3.10 - 3.15 pm time. market order type for forced square off given pct loss is within acceptable range
                // do it before 3.15, otherwise upstox will try to squareoff on its own anytime between 3.15 - 3.30
                if (MarketUtils.IsTimeAfter310() && !MarketUtils.IsTimeAfter315() && squareOffAllPositionsAtEOD && !isEODMinLossSquareOffMarketOrderUpdated)
                {
                    var ltp = GetLTP();
                    ordPriceType = OrderPriceType.MARKET;

                    var diff = Math.Abs((ltp - todayOutstandingPrice) / ltp);
                    if (diff < pctMaxLossSquareOffPositions && todayOutstandingPrice > goodPrice)
                    {
                        Trace(string.Format("TrySquareOffNearEOD: max loss % {0} is within acceptable range of {1} and avg outstanding price {2} is greater than good price of {3}. Update squareoff at MARKET price type.", diff, pctMaxLossSquareOffPositions, todayOutstandingPrice, goodPrice));
                        doUpdateOrders = true;
                        isEODMinLossSquareOffMarketOrderUpdated = true;
                    }
                }

                // 3 - 3.10 pm time. try simple limit order with min profit price
                else if (!isEODMinProfitSquareOffLimitOrderUpdated)
                {
                    Trace(string.Format("TrySquareOffNearEOD: Update squareoff LIMIT order with min profit % price or cancel outstanding orders"));
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
                            errCode = CancelEquityOrder("(EOD squareoff) @ " + ordPriceType, ref todayOutstandingBuyOrderRef, OrderDirection.BUY);
                        }

                        // bought qty needs square off. there is outstanding sell order, revise the price to try square off 
                        if (!string.IsNullOrEmpty(todayOutstandingSellOrderRef))
                        {
                            // cancel existing sell order
                            errCode = CancelEquityOrder("(EOD squareoff) @ " + ordPriceType, ref todayOutstandingSellOrderRef, OrderDirection.SELL);

                            if (errCode == BrokerErrorCode.Success)
                            {
                                todayOutstandingSellOrderRef = "";
                                // place new sell order, update sell order ref
                                Trace("Placing EOD squareoff updated order");
                                var sellPrice = GetSellPrice(todayOutstandingPrice, false, true);
                                errCode = PlaceEquityOrder(stockCode, todayOutstandingQty, sellPrice.ToString(), ordPriceType, OrderDirection.SELL, EquityOrderType.MARGIN, exchange, out todayOutstandingSellOrderRef);
                            }
                        }
                    }
                    else if (algoType == AlgoType.SimultaneousBuySell)
                    {
                        // if there is an outstanding order pair, just cancel both
                        if (!string.IsNullOrEmpty(todayOutstandingBuyOrderRef) && !string.IsNullOrEmpty(todayOutstandingSellOrderRef))
                        {
                            // cancel existing sell order
                            errCode = CancelEquityOrder("(EOD squareoff) @ " + ordPriceType, ref todayOutstandingSellOrderRef, OrderDirection.SELL);

                            // cancel existing buy order
                            errCode = CancelEquityOrder("(EOD squareoff) @ " + ordPriceType, ref todayOutstandingBuyOrderRef, OrderDirection.BUY);
                        }
                        
                        // bought qty needs square off.  there is outstanding sell order, revise the price to try square off 
                        else if (string.IsNullOrEmpty(todayOutstandingBuyOrderRef) && !string.IsNullOrEmpty(todayOutstandingSellOrderRef))
                        {
                            // cancel existing sell order
                            errCode = CancelEquityOrder("(EOD squareoff) @ " + ordPriceType, ref todayOutstandingSellOrderRef, OrderDirection.SELL);

                            if (errCode == BrokerErrorCode.Success)
                            {
                                todayOutstandingSellOrderRef = "";
                                // place new sell order, update sell order ref
                                Trace("Placing EOD squareoff updated order");
                                var sellPrice = GetSellPrice(todayOutstandingPrice, false, true);
                                errCode = PlaceEquityOrder(stockCode, ordQty, sellPrice.ToString(), ordPriceType, OrderDirection.SELL, EquityOrderType.MARGIN, exchange, out todayOutstandingSellOrderRef);
                            }
                        }

                        // sold qty needs square off. there is outstanding buy order, revise the price to try square off 
                        else if (string.IsNullOrEmpty(todayOutstandingSellOrderRef) && !string.IsNullOrEmpty(todayOutstandingBuyOrderRef))
                        {
                            // cancel existing buy order
                            errCode = CancelEquityOrder("(EOD squareoff) @ " + ordPriceType, ref todayOutstandingBuyOrderRef, OrderDirection.BUY);

                            if (errCode == BrokerErrorCode.Success)
                            {
                                todayOutstandingBuyOrderRef = "";
                                // place new buy order, update buy order ref
                                Trace("Placing EOD squareoff updated order");
                                var buyPrice = GetBuySquareOffPrice(todayOutstandingPrice);
                                errCode = PlaceEquityOrder(stockCode, ordQty, buyPrice.ToString(), ordPriceType, OrderDirection.BUY, EquityOrderType.MARGIN, exchange, out todayOutstandingBuyOrderRef);
                            }
                        }
                    }
                }
            }
        }

        public void CancelHoldingSellOrder()
        {
            if (!string.IsNullOrEmpty(holdingSellOrderRef))
            { 
                var errorCode = CancelEquityOrder("(Holding sell order cancel)", ref holdingSellOrderRef, OrderDirection.SELL);
                UpdatePositionFile(holdingOutstandingQty, holdingOutstandingPrice, "");
            }
        }

        // holding is merged with today's outtsanding and an avg price is arrived at. this then is updated as holding into positions file
        public void ConvertToDeliveryAndUpdatePositionFile(bool isEODLast = false)
        {
            // convert to delivery any open buy position
            if (todayOutstandingQty > 0)
            {
                // convert to delivery, update holding qty and write to positions file
                errCode = ConvertPendingMarginPositionsToDelivery(stockCode, todayOutstandingQty, todayOutstandingQty, settlementNumber, OrderDirection.BUY, exchange);

                if (errCode == BrokerErrorCode.Success || isEODLast)
                {
                    holdingOutstandingPrice = (todayOutstandingPrice * todayOutstandingQty) + (holdingOutstandingQty * holdingOutstandingPrice);
                    holdingOutstandingQty += todayOutstandingQty;
                    holdingOutstandingPrice /= holdingOutstandingQty;
                    holdingOutstandingPrice = Math.Round(holdingOutstandingPrice, 2);

                    UpdatePositionFile(holdingOutstandingQty, holdingOutstandingPrice, "");

                    todayOutstandingQty = 0;
                }
            }
        }

        public void PauseBetweenTradeBookCheck()
        {
            Thread.Sleep(1000 * 60);
        }
    }
}
