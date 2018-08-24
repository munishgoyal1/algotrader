using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StockTrader.Utilities
{
    public static class GeneralUtils
    {
        public static DateTime RoundUp(DateTime dt, TimeSpan d)
        {
            return new DateTime(((dt.Ticks + d.Ticks - 1) / d.Ticks) * d.Ticks);
        }

        public static DateTime GetTodayDateTime(string timeStr)
        {
            var now = DateTime.Now;
            var dateTime = DateTime.ParseExact(timeStr, "HH:mm:ss", null, System.Globalization.DateTimeStyles.None);

            //if (now > dateTime)
            //    dateTime = dateTime.AddDays(1);

            return dateTime;
        }
        
    }
}
