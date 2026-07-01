using System;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DirectoryManager.Data.DbContextInfo
{
    /// <summary>
    /// Forces every DateTime to be treated as UTC. On write, Local values are converted to
    /// UTC and Unspecified/UTC values are stamped as UTC (the app stores UTC wall-clock, and
    /// so did SQL Server). On read, values come back with Kind=Utc. This lets DateTime map to
    /// 'timestamp with time zone' with no shift and no Npgsql "Kind=Unspecified" errors.
    /// </summary>
    public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter()
            : base(
                v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
        {
        }
    }
}
