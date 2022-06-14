using System;
using NodaTime;

namespace OneSTools.EventLog
{
    internal static class DateTimeZoneExtensions
    {
        public static DateTime ToUtc(this DateTimeZone dateTimeZone, DateTime dateTime)
        {
            return LocalDateTime.FromDateTime(dateTime).InZoneLeniently(dateTimeZone).ToDateTimeUtc();
        }
    }
}