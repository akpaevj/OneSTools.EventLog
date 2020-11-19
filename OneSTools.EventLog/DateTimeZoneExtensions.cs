using NodaTime;
using System;
using System.Collections.Generic;
using System.Text;

namespace OneSTools.EventLog
{
    internal static class DateTimeZoneExtensions
    {
        public static DateTime ToUtc(this DateTimeZone dateTimeZone, DateTime dateTime)
            => LocalDateTime.FromDateTime(dateTime).InZoneStrictly(dateTimeZone).ToDateTimeUtc();
    }
}
