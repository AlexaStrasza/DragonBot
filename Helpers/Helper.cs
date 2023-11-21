using DSharpPlus.Entities;
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
        private static ConfigRanks _configRanks;

        static Helper()
        {
            _configRanks = ConfigLoader.LoadConfig();
        }

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

        public static bool IsAdmin(DiscordGuild guild, DiscordMember member)
        {
            return member.Roles.Contains(guild.GetRole(1132893402204213351)) ||
                member.Roles.Contains(guild.GetRole(1133071740797472808)) ||
                member.Roles.Contains(guild.GetRole(1139575841228070972)) ||
                member.Roles.Contains(guild.GetRole(1174393813968629822)) ||
                member.Roles.Contains(guild.GetRole(1138108787128021073));

        }

        public static Rank FindAppropriateRank(int userPoints)
        {
            Rank selectedRank = new Rank();

            foreach (var rank in _configRanks.Ranks)
            {
                if (userPoints >= rank.PointRequirement && (selectedRank.PointRequirement == 0 || rank.PointRequirement > selectedRank.PointRequirement))
                {
                    selectedRank = rank;
                }
            }

            return selectedRank;
        }

        public static Rank GetNextRank(int userPoints)
        {
            Rank nextRank = new Rank() { IsDefault = true };

            foreach (var rank in _configRanks.Ranks)
            {
                if (userPoints < rank.PointRequirement)
                {
                    if (nextRank.IsDefault || rank.PointRequirement < nextRank.PointRequirement)
                    {
                        nextRank = rank;
                        nextRank.IsDefault = false;
                        break;
                    }
                }
            }

            return nextRank;
        }

        public static long ConvertToUnixTimestamp(DateTime dateTime)
        {
            DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan timeSpan = dateTime.ToUniversalTime() - unixEpoch;
            return (long)timeSpan.TotalSeconds;
        }
    }
}
