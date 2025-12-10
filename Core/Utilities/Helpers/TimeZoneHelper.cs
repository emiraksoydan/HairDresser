using System;

namespace Core.Utilities.Helpers
{
    /// <summary>
    /// Centralized timezone helper to avoid repeated TimeZoneInfo lookups
    /// </summary>
    public static class TimeZoneHelper
    {
        private static readonly Lazy<TimeZoneInfo> _turkeyTimeZone = new Lazy<TimeZoneInfo>(() =>
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
            }
            catch
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
            }
        });

        public static TimeZoneInfo TurkeyTimeZone => _turkeyTimeZone.Value;

        public static DateTime ToTurkeyTime(DateTime utcTime)
        {
            return TimeZoneInfo.ConvertTime(utcTime, TimeZoneInfo.Utc, TurkeyTimeZone);
        }

        public static DateTime ToUtcTime(DateTime turkeyTime)
        {
            return TimeZoneInfo.ConvertTime(turkeyTime, TurkeyTimeZone, TimeZoneInfo.Utc);
        }
    }
}

