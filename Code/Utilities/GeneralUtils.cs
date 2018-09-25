using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string GetCurrentMethod()
        {
            var st = new StackTrace();
            var sf = st.GetFrame(1);

            return sf.GetMethod().Name;
        }
    }
}
