using System;
using System.Collections.Generic;

namespace Learnest.Services
{
    /// <summary>
    /// Gregorian only date arithmetic for the built-in perpetual calendar.  Learnest deliberately
    /// ships no lunar or public-holiday data: the calendar is a pure Proleptic Gregorian calendar
    /// with weeks, so it needs no network access and never goes stale.
    /// </summary>
    public static class CalendarService
    {
        public static readonly string[] WeekdayShort = new string[] { "一", "二", "三", "四", "五", "六", "日" };
        public static readonly string[] WeekdayLong = new string[] { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
        public static readonly string[] MonthNames = new string[]
        {
            "一月", "二月", "三月", "四月", "五月", "六月",
            "七月", "八月", "九月", "十月", "十一月", "十二月"
        };

        /// <summary>Monday based index 0..6 for a date.</summary>
        public static int WeekdayIndex(DayOfWeek day)
        {
            return ((int)day + 6) % 7;
        }

        public static string WeekdayName(DayOfWeek day)
        {
            return WeekdayLong[WeekdayIndex(day)];
        }

        public static string WeekdayShortName(DayOfWeek day)
        {
            return WeekdayShort[WeekdayIndex(day)];
        }

        public static bool IsWeekend(DateTime date)
        {
            return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
        }

        /// <summary>Monday of the week containing <paramref name="date"/>.</summary>
        public static DateTime StartOfWeek(DateTime date)
        {
            return date.Date.AddDays(-WeekdayIndex(date.DayOfWeek));
        }

        public static DateTime StartOfMonth(DateTime date)
        {
            return new DateTime(date.Year, date.Month, 1);
        }

        public static DateTime EndOfMonth(DateTime date)
        {
            return new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
        }

        /// <summary>ISO-8601 week number (weeks start on Monday, week 1 contains the first Thursday).</summary>
        public static int IsoWeekNumber(DateTime date)
        {
            System.Globalization.Calendar calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;
            return calendar.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        /// <summary>
        /// A calendar month laid out as Monday-first weeks.  Always returns whole weeks, padding
        /// with the neighbouring months so the grid is rectangular.
        /// </summary>
        public static List<List<DateTime>> BuildMonthGrid(int year, int month)
        {
            List<List<DateTime>> weeks = new List<List<DateTime>>();
            DateTime first = new DateTime(year, month, 1);
            DateTime start = StartOfWeek(first);
            DateTime last = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            DateTime end = StartOfWeek(last).AddDays(6);

            List<DateTime> week = new List<DateTime>(7);
            for (DateTime day = start; day <= end; day = day.AddDays(1))
            {
                week.Add(day);
                if (week.Count == 7)
                {
                    weeks.Add(week);
                    week = new List<DateTime>(7);
                }
            }
            if (week.Count > 0) { weeks.Add(week); }
            return weeks;
        }

        /// <summary>Six whole weeks covering a month, for a stable height calendar grid.</summary>
        public static List<DateTime> MonthCells(int year, int month, int weekCount)
        {
            List<DateTime> cells = new List<DateTime>(weekCount * 7);
            DateTime start = StartOfWeek(new DateTime(year, month, 1));
            for (int i = 0; i < weekCount * 7; i++) { cells.Add(start.AddDays(i)); }
            return cells;
        }

        public static DateTime AddMonths(DateTime date, int months)
        {
            return date.AddMonths(months);
        }

        public static string FormatDate(DateTime date)
        {
            return date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string FormatDateShort(DateTime date)
        {
            return date.ToString("MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string FormatDateTime(DateTime date)
        {
            return date.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string FormatFull(DateTime date)
        {
            return date.ToString("yyyy年M月d日", System.Globalization.CultureInfo.InvariantCulture) + " " + WeekdayName(date.DayOfWeek);
        }

        /// <summary>Human friendly distance to a deadline, e.g. "3 天后" or "已逾期 2 小时".</summary>
        public static string DescribeDistance(DateTime target, DateTime now)
        {
            TimeSpan span = target - now;
            bool past = span.Ticks < 0;
            TimeSpan abs = past ? span.Negate() : span;
            string amount;
            if (abs.TotalDays >= 365) { amount = (abs.TotalDays / 365.0).ToString("0.#") + " 年"; }
            else if (abs.TotalDays >= 60) { amount = (abs.TotalDays / 30.0).ToString("0.#") + " 个月"; }
            else if (abs.TotalDays >= 1) { amount = ((int)abs.TotalDays) + " 天"; }
            else if (abs.TotalHours >= 1) { amount = ((int)abs.TotalHours) + " 小时 " + abs.Minutes + " 分"; }
            else if (abs.TotalMinutes >= 1) { amount = ((int)abs.TotalMinutes) + " 分钟"; }
            else { amount = "不到 1 分钟"; }
            return past ? "已逾期 " + amount : amount + "后";
        }

        /// <summary>Compact span label used by the timeline bars.</summary>
        public static string DescribeSpan(TimeSpan span)
        {
            double days = span.TotalDays;
            if (days >= 365) { return (days / 365.0).ToString("0.#") + " 年"; }
            if (days >= 30) { return (days / 30.0).ToString("0.#") + " 个月"; }
            if (days >= 1) { return days.ToString("0.#") + " 天"; }
            if (span.TotalHours >= 1) { return span.TotalHours.ToString("0.#") + " 小时"; }
            return Math.Max(1, (int)span.TotalMinutes) + " 分钟";
        }

        /// <summary>Pick a sensible tick interval (in days) for a given pixels-per-day zoom.</summary>
        public static int SuggestTickDays(double pixelsPerDay)
        {
            double[] candidates = new double[] { 1, 2, 3, 7, 14, 30, 60, 90, 180, 365 };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] * pixelsPerDay >= 64) { return (int)candidates[i]; }
            }
            return 365;
        }
    }
}
