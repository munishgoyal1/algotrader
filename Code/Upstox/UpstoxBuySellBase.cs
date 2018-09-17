using System;
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
        public OrderPositionTypeEnum Type;
        public string SettlementNumber;
        public int Qty;
        public string OrderId;
        public OrderStatus Status;
    }

    public class UpstoxBuySellBase
    {
        public double buyPriceCap;
        public double goodPrice;
        public EquityOrderType orderType;
        public double pctExtraMarkdownForAveraging;
        public double buyMarkdownFromLcpDefault;
        public double sellMarkupDefault;
        public double sellMarkupForDelivery;
        public double sellMarkupForMinProfit;
        public double sellMarkupForEODInsufficientLimitSquareOff;
        public double pctSquareOffForMinProfit;
        public bool placeBuyNoLtpCompare;
        public bool squareOffAllPositionsAtEOD;
        public double pctMaxLossSquareOffPositions;
        public bool useAvgBuyPriceInsteadOfLastBuyPriceToCalculateBuyPriceForNewOrder;
        public bool doConvertToDeliveryAtEOD;
        public bool doSquareOffIfInsufficientLimitAtEOD;

        public DateTime startTime;
        public DateTime endTime;

        public MyUpstoxWrapper myUpstoxWrapper = null;
        public string stockCode = null;
        public string isinCode = null;
        public int ordQty = 0;
        public int maxTotalOutstandingQtyAllowed = 0;
        public int maxTodayOutstandingQtyAllowed = 0;
        public int maxBuyOrdersAllowedInADay = 0;
        public Exchange exchange;
        public string exchStr;
        public string positionFile;

        public double holdingOutstandingPrice = 0;
        public int holdingOutstandingQty = 0;
        public double todayOutstandingPrice = 0;
        public int todayOutstandingQty = 0;

        public List<HoldingOrder> holdingsOrders = new List<HoldingOrder>();

        public string todayOutstandingBuyOrderId = ""; // outstanding buy order Id
        public string todayOutstandingSellOrderId = "";// outstanding sell order Id
        public bool isEODMinProfitSquareOffLimitOrderUpdated = false;
        public bool isEODMinLossSquareOffMarketOrderUpdated = false;
        public bool isEODOutstandingPositionConverted = false;
        public string settlementNumber = "";

        public double lastBuyPrice = 0;
        public bool isFirstBuyOrder = true;

        public int todayBuyOrderCount = 0;
        public const string orderTraceFormat = "{4} Order: {5} {0} {1} {2} @ {3} {6}";
        public const string orderCancelTraceFormat = "{0}: {1} {2} {3}: {4}";
        public const string tradeTraceFormat = "{4} Trade: {0} {1} {2} @ {3}";
        public const string deliveryTraceFormat = "Conversion to delivery: {0} {1} qty of {2}";
        public const string positionFileTotalQtyPriceFormat = "{0} {1}\n";
        public const string positionFileLineQtyPriceFormat = "{0} {1} {2} {3} {4}";

        public BrokerErrorCode errCode;

        public double Ltp;

        public virtual void StockBuySell()
        {

        }
        public UpstoxBuySellBase(UpstoxTradeParams tradeParams)
        {
            myUpstoxWrapper = tradeParams.upstox;
            stockCode = tradeParams.stockCode;
            isinCode = tradeParams.isinCode;
            ordQty = tradeParams.ordQty;
            maxTotalOutstandingQtyAllowed = tradeParams.maxTotalOutstandingQtyAllowed;
            maxTodayOutstandingQtyAllowed = tradeParams.maxTodayOutstandingQtyAllowed;
            maxBuyOrdersAllowedInADay = tradeParams.maxBuyOrdersAllowedInADay;
            exchange = tradeParams.exchange;
            exchStr = exchange == Exchange.NSE ? "NSE_EQ" : "BSE_EQ";
            positionFile = AlgoUtils.GetPositionFile(stockCode);

            buyPriceCap = tradeParams.buyPriceCap;
            goodPrice = tradeParams.goodPrice;
            orderType = tradeParams.orderType;
            pctExtraMarkdownForAveraging = tradeParams.pctExtraMarkdownForAveraging;
            buyMarkdownFromLcpDefault = tradeParams.buyMarkdownFromLcpDefault;
            sellMarkupDefault = tradeParams.sellMarkupForMargin;
            sellMarkupForDelivery = tradeParams.sellMarkupForDelivery;
            sellMarkupForMinProfit = tradeParams.sellMarkupForMinProfit;
            sellMarkupForEODInsufficientLimitSquareOff = tradeParams.sellMarkupForEODInsufficientLimitSquareOff;
            placeBuyNoLtpCompare = tradeParams.placeBuyNoLtpCompare;
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

            // just place squareoff orders for previous days open positions pending for delivery - develop it later

            // Position file always contains the holding qty, holding price and different type of holdings' details (demat/btst, qty, sell order ref) etc
            ReadPositionFile();

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

            // get all holding qty trades
            var holdingTradesRef = holdingsOrders.Select(h => h.OrderId);

            // any outstanding qty (buy minus sell trades) today except from holding qty trade
            todayOutstandingQty = myUpstoxWrapper.GetNetQty(exchStr, stockCode);

            var buyTrades = trades.Values.Where(t => t.Direction == OrderDirection.BUY).OrderByDescending(t => t.DateTime).ToList();

            //sort the trade book and get last buy price
            lastBuyPrice = myUpstoxWrapper.GetBoughtQty(exchStr, stockCode) > 0 ? buyTrades.First().Price : holdingOutstandingPrice;

            var numOutstandingBuyTrades = todayOutstandingQty > 0 ? todayOutstandingQty / ordQty : 0;
            // these are latest trades taken. each buy trade is for single lot and thus for each lot there is a trade
            todayOutstandingPrice = numOutstandingBuyTrades == 0 ? 0 : buyTrades.Take(numOutstandingBuyTrades).Average(t => t.Price);

            ProcessHoldingSellOrderExecution(trades);

            var buyOrders = orders.Values.Where(o => o.Direction == OrderDirection.BUY && o.Status == OrderStatus.ORDERED);
            var sellOrders = orders.Values.Where(o => o.Direction == OrderDirection.SELL && o.Status == OrderStatus.ORDERED && !holdingTradesRef.Contains(o.OrderId)); // pick only today's outstanding related sell orders

            // assumed that there is always at max only single outstanding buy order and a at max single outstanding sell order
            todayOutstandingBuyOrderId = buyOrders.Any() ? buyOrders.First().OrderId : "";
            todayOutstandingSellOrderId = sellOrders.Any() ? sellOrders.First().OrderId : "";

            var sellOrder = sellOrders.Any() ? sellOrders.First() : null;

            isFirstBuyOrder = string.IsNullOrEmpty(todayOutstandingBuyOrderId) && !trades.Where(t => t.Value.Direction == OrderDirection.BUY).Any();

            // if sqoff sell order for holdings is needed then place it 
            //assumption is: if there is a holding pending from day before then it would have been converted to delivery
            if (holdingOutstandingQty > 0)
            {
                // get holdings
                List<EquityDematHoldingRecord> dematHoldings;
                errCode = myUpstoxWrapper.GetHoldings(stockCode, out dematHoldings);

                if (errCode != BrokerErrorCode.Success)
                    throw new Exception("Failed getting Holdings at Init");

                // place sq off sell order, update sell order ref
                var sellPrice = GetSellPrice(holdingOutstandingPrice, true, false);
                string sellOrderId = "";
                if (dematHoldings.Any())
                {
                    var dematHolding = dematHoldings.First();
                    if (dematHolding.BlockedQuantity < holdingOutstandingQty)
                    {
                        var pendingQty = holdingOutstandingQty - dematHolding.BlockedQuantity;
                        errCode = PlaceEquityOrder(exchStr, stockCode, OrderDirection.SELL, OrderPriceType.LIMIT, pendingQty, EquityOrderType.DELIVERY, sellPrice, out sellOrderId);
                        if (errCode == BrokerErrorCode.Success)
                        {
                            holdingsOrders.Add(new HoldingOrder { Type = OrderPositionTypeEnum.Demat, OrderId = sellOrderId, Qty = pendingQty, SettlementNumber = "" });
                        }
                    }
                }

                UpdatePositionFile();
            }

            // For AverageTheBuyThenSell algo - place sell order for outstanding qty if not already present
            if (algoType == AlgoType.AverageTheBuyThenSell)
            {
                // check if the outstanding sell order has matching qty or not
                if (sellOrder != null && !string.IsNullOrEmpty(todayOutstandingSellOrderId) && sellOrder.Quantity < todayOutstandingQty)
                {
                    // Cancel existing sell order 
                    errCode = CancelEquityOrder("[Init Update Sell Qty]", ref todayOutstandingSellOrderId, orderType, OrderDirection.SELL);
                }

                if (string.IsNullOrEmpty(todayOutstandingSellOrderId) && todayOutstandingQty > 0)
                {
                    var sellPrice = GetSellPrice(todayOutstandingPrice, false, false);
                    errCode = PlaceEquityOrder(exchStr, stockCode, OrderDirection.SELL, OrderPriceType.LIMIT, todayOutstandingQty, orderType, sellPrice, out todayOutstandingSellOrderId);
                }
            }

            todayBuyOrderCount = buyOrders.Count();
        }

        protected void ProcessHoldingSellOrderExecution(Dictionary<string, EquityTradeBookRecord> trades)
        {
            var holdingTradesId = holdingsOrders.Select(h => h.OrderId);

            if (!holdingTradesId.Any(htr => trades.ContainsKey(htr)))
                return;

            var holdingsOrdersToRemove = new List<HoldingOrder>();

            foreach (var holdingOrder in holdingsOrders.Where(ho => ho.Qty > 0 && !string.IsNullOrEmpty(ho.OrderId)))
            {
                var orderRef = holdingOrder.OrderId;

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
                        holdingOrder.Status = OrderStatus.COMPLETED;
                        //holdingsOrdersToRemove.Add(holdingOrder);
                    }
                }
            }

            holdingsOrders.RemoveAll(h => holdingsOrdersToRemove.Contains(h));

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
            out string orderId)
        {
            BrokerErrorCode errCode = BrokerErrorCode.Unknown;
            orderId = "";

            errCode = myUpstoxWrapper.PlaceEquityOrder(exchange, stockCode, orderDirection, orderPriceType, quantity, orderType, price, out orderId);

            Trace(string.Format(orderTraceFormat, stockCode, orderDirection, quantity, price, orderType, errCode, orderPriceType));

            if (errCode == BrokerErrorCode.Success && orderDirection == OrderDirection.BUY)
            {
                todayBuyOrderCount++;
                if (todayBuyOrderCount >= maxBuyOrdersAllowedInADay)
                    Trace(string.Format("Buy order count reached is: {0}. Max buy orders allowed: {1}", todayBuyOrderCount, maxBuyOrdersAllowedInADay));
            }

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
                        holdingOrder.OrderId = split[3].Trim();
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
                    var positionLineStr = string.Format(positionFileLineQtyPriceFormat, holdingOrder.Type, holdingOrder.SettlementNumber, holdingOrder.Qty, holdingOrder.OrderId, holdingOrder.Status);
                    lines.Add(positionLineStr);
                }
            }
            File.WriteAllLines(positionFile, lines);

            if (isEODUpdate)
                Trace(string.Format("{2}PositionFile updated as {0} {1}", holdingOutstandingQty, holdingOutstandingPrice, isEODUpdate ? "EOD " : ""));
        }


        // Try min profit squareoff first between 
        // 3 - 3.05 = min profit limit sell order
        // 3.05 - 3.10 = min loss market order (if EOD sqoff enabled and loss within maxloss pct)
        //
        public void TrySquareOffNearEOD(AlgoType algoType)
        {
            string strategy = "[Margin EOD]";

            // if after 3 pm, then try to square off in at least no profit no loss if possible. cancel the outstanding buys anyway
            if (MarketUtils.IsTimeAfter3XMin(0))
            {
                var ordPriceType = OrderPriceType.LIMIT;
                var equityOrderType = orderType;
                var updateSellOrder = false;

                // just cancel the outstanding buy order
                if (algoType == AlgoType.AverageTheBuyThenSell)
                {
                    if (!string.IsNullOrEmpty(todayOutstandingBuyOrderId))
                    {
                        // cancel existing buy order
                        errCode = CancelEquityOrder("[Margin EOD]", ref todayOutstandingBuyOrderId, orderType, OrderDirection.BUY);
                    }
                }

                if (todayOutstandingQty == 0)
                    return;

                // 3.05 - 3.10 pm time. market order type if must sqoff at EOD and given pct loss is within acceptable range
                if (MarketUtils.IsTimeAfter3XMin(5) && !MarketUtils.IsTimeAfter3XMin(10) && squareOffAllPositionsAtEOD && !isEODMinLossSquareOffMarketOrderUpdated)
                {
                    double ltp;
                    var errCode = GetLTP(out ltp);

                    if (errCode != BrokerErrorCode.Success)
                        return;

                    ordPriceType = OrderPriceType.MARKET;

                    var diff = (ltp - todayOutstandingPrice) / ltp;

                    Trace(string.Format("[Margin EOD]: diff {0} ltp {1} outstandingprice {2} pctMaxLossSquareOffPositions {3} goodPrice {4} ", diff, ltp, todayOutstandingPrice, pctMaxLossSquareOffPositions, goodPrice));

                    if (diff > pctMaxLossSquareOffPositions && todayOutstandingPrice > goodPrice)
                    {
                        strategy = string.Format("[Margin EOD]: max loss {0}% is less than {1}% and avg outstanding price {2} is greater than good price of {3}. LTP is {4}. Place squareoff @ MARKET.", diff, pctMaxLossSquareOffPositions, todayOutstandingPrice, goodPrice, ltp);
                        updateSellOrder = true;
                        isEODMinLossSquareOffMarketOrderUpdated = true;
                    }
                }

                // 3.00 - 3.05 pm time. try simple limit order with min profit price. watch until 3.10 pm
                else if (!isEODMinProfitSquareOffLimitOrderUpdated)
                {
                    strategy = string.Format("[Margin EOD]: MinProfit Squareoff using limit sell order");
                    ordPriceType = OrderPriceType.LIMIT;
                    isEODMinProfitSquareOffLimitOrderUpdated = true;
                    updateSellOrder = true;
                }

                // Assuming the position and the sqoff order are same qty (i.e. in sync as of now)
                // for already DELIVERY type sq off order, no need to do anything. Either this logic has already run or the stock was started in DELIVERY mode from starting itself
                else if(MarketUtils.IsTimeAfter3XMin(10) && !isEODOutstandingPositionConverted && orderType == EquityOrderType.MARGIN)
                {
                    List<EquityPositionRecord> positions;
                    errCode = myUpstoxWrapper.GetPositions(stockCode, out positions);

                    if (errCode == BrokerErrorCode.Success)
                    {
                        var position = positions.Where(p => p.Exchange == exchStr && p.EquityOrderType == EquityOrderType.DELIVERY).FirstOrDefault();
                        // check if position is converted to delivery, then cancel sq off order and place sell delivery order
                        if (position != null)
                        {
                            strategy = string.Format("[Margin EOD]: Position converted. Converting the sq off order to DELIVERY");
                            equityOrderType = EquityOrderType.DELIVERY;
                            ordPriceType = OrderPriceType.LIMIT;
                            isEODOutstandingPositionConverted = true;
                            updateSellOrder = true;
                        }
                    }
                }

                if (updateSellOrder)
                {
                    if (algoType == AlgoType.AverageTheBuyThenSell)
                    {
                        // bought qty needs square off. there is outstanding sell order, revise the price to try square off 
                        if (!string.IsNullOrEmpty(todayOutstandingSellOrderId))
                        {
                            Trace(strategy);
                            // cancel existing sell order
                            errCode = CancelEquityOrder("[Margin EOD]", ref todayOutstandingSellOrderId, orderType, OrderDirection.SELL);

                            if (errCode == BrokerErrorCode.Success)
                            {
                                // place new sell order, update sell order ref
                                var sellPrice = GetSellPrice(todayOutstandingPrice, false, true);
                                errCode = PlaceEquityOrder(exchStr, stockCode, OrderDirection.SELL, ordPriceType, todayOutstandingQty, equityOrderType, sellPrice, out todayOutstandingSellOrderId);
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
                if (!string.IsNullOrEmpty(holdingOrder.OrderId))
                {
                    errCode = CancelEquityOrder(string.Format("[Holding EOD] {0} {1}", holdingOrder.Type, holdingOrder.SettlementNumber), ref holdingOrder.OrderId, EquityOrderType.DELIVERY, OrderDirection.SELL);

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

        // holding is merged with today's outtsanding and an avg price is arrived at. this then is updated as holding into positions file
        public void ConvertToDeliveryAndUpdatePositionFile(bool isEODLast = false)
        {
            var errCode = BrokerErrorCode.Unknown;
            bool isConversionSuccessful = false;

            // convert to delivery any open buy position
            if (todayOutstandingQty > 0 && doConvertToDeliveryAtEOD && orderType == EquityOrderType.MARGIN)
            {
                // cancel outstanding order to free up the qty for conversion
                if (!string.IsNullOrEmpty(todayOutstandingSellOrderId))
                    errCode = CancelEquityOrder("[Margin Conversion EOD]", ref todayOutstandingSellOrderId, orderType, OrderDirection.SELL);

                // convert to delivery, update holding qty and write to positions file
                // may need to seperate out and convert each position seperately. currently all outstanding for the stock is tried to convert in single call
                if (string.IsNullOrEmpty(todayOutstandingSellOrderId))
                    errCode = ConvertPendingMarginPositionsToDelivery(stockCode, todayOutstandingQty, todayOutstandingQty, settlementNumber, OrderDirection.BUY, exchStr);

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
                    if (!string.IsNullOrEmpty(todayOutstandingSellOrderId))
                        errCode1 = CancelEquityOrder("[Margin EOD] Insufficient Limit to convert. Try to Squareoff", ref todayOutstandingSellOrderId, orderType, OrderDirection.SELL);

                    if (errCode1 == BrokerErrorCode.Success)
                    {
                        // place new sell order, update sell order ref
                        var sellPrice = GetSellPrice(todayOutstandingPrice, false, false, true);
                        errCode1 = PlaceEquityOrder(exchStr, stockCode, OrderDirection.SELL, OrderPriceType.LIMIT, todayOutstandingQty, orderType, sellPrice, out todayOutstandingSellOrderId);
                    }
                }
            }

            if (isConversionSuccessful || orderType == EquityOrderType.DELIVERY || isEODLast)
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

        public BrokerErrorCode ConvertPendingMarginPositionsToDelivery(string stockCode,
            int openQty,
            int toConvertQty,
            string settlementRef,
            OrderDirection ordDirection,
            string exchange)
        {
            var errCode = myUpstoxWrapper.ConvertToDeliveryFromMarginOpenPositions(stockCode, openQty, toConvertQty, settlementNumber, ordDirection, exchange);
            Trace(string.Format(deliveryTraceFormat, errCode, stockCode, toConvertQty));

            return errCode;
        }
        public void PauseBetweenTradeBookCheck()
        {
            Thread.Sleep(1000 * 30);
        }
    }
}
