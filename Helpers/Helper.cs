using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DragonBot.Helpers
{
    public static class Helper
    {
        public static string GetNumbers(this string text)
        {
            text ??= string.Empty;
            return new string(text.Where(p => char.IsDigit(p)).ToArray());
        }

        public static bool IsSameWeek(DateTime date1, DateTime date2)
        {
            // Calculate the week number and year for both dates
            int weekNumber1 = GetIso8601WeekOfYear(date1);
            int weekNumber2 = GetIso8601WeekOfYear(date2);
            int year1 = date1.Year;
            int year2 = date2.Year;

            // Compare the year and week number to determine if they are in the same week
            return year1 == year2 && weekNumber1 == weekNumber2;
        }

        public static int GetIso8601WeekOfYear(DateTime time)
        {
            // Seriously cheat.  If its Monday, Tuesday or Wednesday, then it'll 
            // be the same week# as whatever Thursday, Friday or Saturday are,
            // and we always get those right
            DayOfWeek day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(time);
            if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
            {
                time = time.AddDays(3);
            }

            // Return the week of our adjusted day
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(time, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }
    }
}
