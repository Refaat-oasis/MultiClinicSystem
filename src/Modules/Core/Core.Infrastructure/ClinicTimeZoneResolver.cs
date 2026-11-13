namespace Core.Infrastructure;

/// <summary>
/// Resolves clinic IANA or Windows time zone IDs safely to TimeZoneInfo with UTC fallback.
/// </summary>
internal static class ClinicTimeZoneResolver
{
    public static TimeZoneInfo Resolve(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId.Trim(), out TimeZoneInfo? tz))
            {
                return tz;
            }

            if (string.Equals(timeZoneId.Trim(), "Africa/Cairo", StringComparison.OrdinalIgnoreCase) &&
                TimeZoneInfo.TryFindSystemTimeZoneById("Egypt Standard Time", out TimeZoneInfo? egypt))
            {
                return egypt;
            }
        }

        return TimeZoneInfo.Utc;
    }
}

