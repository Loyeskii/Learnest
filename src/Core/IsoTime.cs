using System;
using System.Text;

namespace Learnest.Core
{
    /// <summary>
    /// A tiny, strict ISO-8601 style date/time helper.  Learnest stores every timestamp
    /// as a sortable local-time string ("yyyy-MM-ddTHH:mm:ss") because all of its data is
    /// personal and local.  The same format is used by JavaScript's Date.toISOString
    /// prefix, so profiles stay readable and hand-editable.
    /// </summary>
    public static class IsoTime
    {
        public const string SortableFormat = "yyyy-MM-ddTHH:mm:ss";
        public const string DateFormat = "yyyy-MM-dd";

        private static readonly DateTime Epoch = new DateTime(2000, 1, 1);

        public static string Now() { return Format(DateTime.Now); }

        public static string Format(DateTime value)
        {
            return value.ToString(SortableFormat, System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string FormatDate(DateTime value)
        {
            return value.ToString(DateFormat, System.Globalization.CultureInfo.InvariantCulture);
        }

        public static DateTime Parse(string text, DateTime fallback)
        {
            DateTime parsed;
            if (TryParse(text, out parsed)) { return parsed; }
            return fallback;
        }

        public static bool TryParse(string text, out DateTime value)
        {
            value = DateTime.MinValue;
            if (string.IsNullOrEmpty(text)) { return false; }
            string s = text.Trim();
            DateTime parsed;
            if (DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out parsed))
            {
                value = parsed;
                return true;
            }
            if (DateTime.TryParse(s, System.Globalization.CultureInfo.CurrentCulture,
                    System.Globalization.DateTimeStyles.None, out parsed))
            {
                value = parsed;
                return true;
            }
            return false;
        }

        public static bool TryParseNullable(string text, out DateTime? value)
        {
            value = null;
            DateTime parsed;
            if (TryParse(text, out parsed)) { value = parsed; return true; }
            return false;
        }

        /// <summary>Human friendly relative description such as "3 天后" or "已逾期 2 小时".</summary>
        public static string Describe(DateTime target, DateTime now)
        {
            TimeSpan span = target - now;
            bool past = span.Ticks < 0;
            TimeSpan abs = past ? span.Negate() : span;
            string amount;
            if (abs.TotalDays >= 365) { amount = Math.Round(abs.TotalDays / 365.0, 1) + " 年"; }
            else if (abs.TotalDays >= 30) { amount = Math.Round(abs.TotalDays / 30.0, 1) + " 个月"; }
            else if (abs.TotalDays >= 1) { amount = ((int)abs.TotalDays) + " 天"; }
            else if (abs.TotalHours >= 1) { amount = ((int)abs.TotalHours) + " 小时"; }
            else if (abs.TotalMinutes >= 1) { amount = ((int)abs.TotalMinutes) + " 分钟"; }
            else { amount = "不到 1 分钟"; }
            if (past) { return "已逾期 " + amount; }
            return amount + "后";
        }

        public static string ShortId(string prefix)
        {
            // Deterministic-length, sortable-ish identifier: prefix + base36 timestamp + random tail.
            long ticks = DateTime.Now.Ticks - Epoch.Ticks;
            StringBuilder sb = new StringBuilder(prefix);
            sb.Append(Base36(ticks / 10000L, 8));
            sb.Append('-');
            sb.Append(Base36(Math.Abs(Guid.NewGuid().GetHashCode()), 5));
            return sb.ToString();
        }

        public static string Base36(long value, int pad)
        {
            const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
            if (value < 0) { value = -value; }
            StringBuilder sb = new StringBuilder();
            do
            {
                sb.Insert(0, digits[(int)(value % 36)]);
                value /= 36;
            } while (value > 0);
            while (sb.Length < pad) { sb.Insert(0, '0'); }
            return sb.ToString();
        }
    }
}
