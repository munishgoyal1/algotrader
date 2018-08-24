
using System;

namespace StockTrader.Common
{
    public class Number
    {
        public readonly double Value;
        public readonly bool IsPercentage;

        public Number(double value,
            bool isPercentage)
        {
            Value = value;
            IsPercentage = isPercentage;
        }
    }

    public delegate void ResetChartHandler(object o, EventArgs e);
    public delegate void AddTickChartDelegate(ChartTickPoint point);

    public enum ChartTickType
    {
        Price,
        Order,
        Peak,
        SquareOffPoint
    }

    public class ChartTickPoint
    {
        public DateTime X;
        public double Y;

        public bool IsBuyOrderOrBottomPeak;
        public ChartTickType TickType;

        public ChartTickPoint(DateTime x, double y, bool isBuyOrBottom, ChartTickType tickType)
        {
            X = x;
            Y = y;
            IsBuyOrderOrBottomPeak = isBuyOrBottom;
            TickType = tickType;
        }
    }
}