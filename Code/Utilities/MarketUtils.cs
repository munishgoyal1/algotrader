
using StockTrader.Platform.Logging;
using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace StockTrader.Utilities
{

    public static class MarketUtils
    {
        private static DateTime marketCloseHours = new DateTime(1, 1, 1, 15, 29, 59);
        private static TimeSpan marketCloseTimeSpan = new TimeSpan(15, 29, 59);

        public static DateTime GetTimeToday(int hour, int minutes)
        {
            var today = DateTime.Today;
            var dateTime = new DateTime(today.Year, today.Month, today.Day, hour, minutes, 0);
            return dateTime;
        }

        public static DateTime GetExpiryExactDate(DateTime date)
        {
            DateTime exactTime = new DateTime(date.Year, date.Month, date.Day);
            exactTime += marketCloseTimeSpan;

            return exactTime;
        }

        public static DateTime GetMarketCloseTime()
        {
            TimeSpan ts1 = DateTime.Now.TimeOfDay;
            TimeSpan ts2 = marketCloseHours.TimeOfDay;

            DateTime EarliestMarketCloseTime = DateTime.Now.Add(ts2 - ts1);

            return EarliestMarketCloseTime;
        }

        public static bool IsTimeAfter(DateTime timeToCompare)
        {
            var r1 = TimeSpan.Compare(DateTime.Now.TimeOfDay, timeToCompare.TimeOfDay);
            return r1 > 0;
        }

        public static bool IsTimeAfter330Market(DateTime date)
        {
            if (date.Hour < 15) return false;
            if (date.Hour > 15) return true;
            if (date.Hour == 15 && date.Minute < 32) return false;
            return true;
        }

        public static bool IsTimeAfter330Market()
        {
            return IsTimeAfter330Market(DateTime.Now);
        }

        public static bool IsTimeAfter3XMin(DateTime date, int min)
        {
            if (date.Hour < 15) return false;
            if (date.Hour > 15) return true;
            if (date.Hour == 15 && date.Minute < min) return false;
            return true;
        }

        public static bool IsTimeAfter3XMin(int min)
        {
            return IsTimeAfter3XMin(DateTime.Now, min);
        }

        public static bool IsTimeAfter2XMin(DateTime date, int min)
        {
            if (date.Hour < 14) return false;
            if (date.Hour > 14) return true;
            if (date.Hour == 14 && date.Minute < min) return false;
            return true;
        }

        public static bool IsTimeAfter2XMin(int min)
        {
            return IsTimeAfter2XMin(DateTime.Now, min);
        }

        public static bool IsTimeAfter325(DateTime date)
        {
            if (date.Hour < 15) return false;
            if (date.Hour > 15) return true;
            if (date.Hour == 15 && date.Minute < 25) return false;
            return true;
        }

        public static bool IsTimeAfter325()
        {
            return IsTimeAfter325(DateTime.Now);
        }

        public static bool IsTimeAfter320(DateTime date)
        {
            if (date.Hour < 15) return false;
            if (date.Hour > 15) return true;
            if (date.Hour == 15 && date.Minute < 20) return false;
            return true;
        }

        public static bool IsTimeAfter320()
        {
            return IsTimeAfter320(DateTime.Now);
        }

        public static bool IsTimeAfter315(DateTime date)
        {
            if (date.Hour < 15) return false;
            if (date.Hour > 15) return true;
            if (date.Hour == 15 && date.Minute < 15) return false;
            return true;
        }

        public static bool IsTimeAfter310(DateTime date)
        {
            if (date.Hour < 15) return false;
            if (date.Hour > 15) return true;
            if (date.Hour == 15 && date.Minute < 10) return false;
            return true;
        }

        public static bool IsTimeAfter310()
        {
            return IsTimeAfter310(DateTime.Now);
        }

        public static bool IsTimeAfter315()
        {
            return IsTimeAfter315(DateTime.Now);
        }

        public static bool IsTimeAfter3()
        {
            return DateTime.Now.Hour >= 15 ? true : false;
        }

        public static bool IsTimeTodayAfter915(DateTime date)
        {
            if (DateTime.Now.Date != date.Date) return false;
            if (date.Hour < 9) return false;
            if (date.Hour > 9) return true;
            if (date.Hour == 9 && date.Minute < 15) return false;

            return true;
        }

        public static bool IsTimeAfter915(DateTime date)
        {
            if (date.Hour < 9) return false;
            if (date.Hour > 9) return true;
            if (date.Hour == 9 && date.Minute < 15) return false;

            return true;
        }

        public static bool IsTimeAfter915()
        {
            return IsTimeAfter915(DateTime.Now);
        }

        public static bool IsTimeAfter920(DateTime date)
        {
            if (date.Hour < 9) return false;
            if (date.Hour > 9) return true;
            if (date.Hour == 9 && date.Minute < 20) return false;

            return true;
        }
        public static bool IsTimeAfter920()
        {
            return IsTimeAfter920(DateTime.Now);
        }

        public static void WaitUntilMarketOpen()
        {
            Console.WriteLine("WaitUntilMarketOpen()");
            while (!IsMarketOpen())
            {
                Thread.Sleep(100);
            }
            Console.WriteLine("Market Open");
        }

        public static bool IsMarketOpen()
        {
            DateTime Now = DateTime.Now;

            if (Now.DayOfWeek == DayOfWeek.Sunday || Now.DayOfWeek == DayOfWeek.Saturday)
            {
                return false;
            }
            if (Now.Hour < 9) return false;
            if (Now.Hour == 9 && Now.Minute < 16) return false;
            if (Now.Hour < 15) return true;
            if (Now.Hour > 15) return false;
            if (Now.Minute < 31) return true;
            return false;
        }

        public static bool IsMarketOpenIncludingPreOpen()
        {
            DateTime Now = DateTime.Now;

            if (Now.DayOfWeek == DayOfWeek.Sunday || Now.DayOfWeek == DayOfWeek.Saturday)
            {
                return false;
            }
            if (Now.Hour < 9) return false;
            if (Now.Hour == 9 && Now.Minute < 1) return false;
            if (Now.Hour < 15) return true;
            if (Now.Hour > 15) return false;
            if (Now.Minute < 31) return true;
            return false;
        }

        public static DateTime GetMarketCurrentDate()
        {
            DateTime Now = DateTime.Now;
            DateTime CurrentDate = Now;

            if (!IsMarketOpen())
            {
                if (Now.DayOfWeek == DayOfWeek.Saturday)
                {
                    CurrentDate = CurrentDate.AddDays(2);
                }
                else if (Now.DayOfWeek == DayOfWeek.Sunday)
                {
                    CurrentDate = CurrentDate.AddDays(1);
                }
                else if (Now.Hour >= 15)
                {
                    CurrentDate = CurrentDate.AddDays(1);
                }
            }
            return CurrentDate;
        }

        public static DateTime GetExpiryDate(string expiryDate)
        {
            return DateTime.ParseExact(expiryDate, "ddMMMyyyy", DateTimeFormatInfo.InvariantInfo);
        }

        public static int GetVolume(string vol)
        {
            double temp;
            double.TryParse(vol, out temp);
            return (int)temp;
        }

        public static double GetPrice(string price)
        {
            double temp;
            double.TryParse(price, out temp);
            return temp;
        }

        public static double GetPercentage(string perc)
        {
            bool isNegative = false;
            if (perc.StartsWith("-"))
                isNegative = true;
            perc = perc.Substring(1);
            double temp = double.Parse(perc);

            if (isNegative)
                temp = -temp;
            return temp;
        }
    }

}
