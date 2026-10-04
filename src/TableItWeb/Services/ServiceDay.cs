namespace TableItWeb.Services;

public static class ServiceDay
{
    public const string DefaultTimeZoneId = "Europe/Copenhagen";
    public const int DefaultStartHour = 5;

    /// <summary>
    /// Start of the current service day in UTC: today at <paramref name="startHour"/> in the restaurant's
    /// time zone, or yesterday at that hour if it is earlier than that. Independent of the host's time zone.
    /// </summary>
    public static DateTime StartUtc(TimeZoneInfo zone, int startHour, DateTime? utcNow = null)
    {
        var nowUtc = DateTime.SpecifyKind(utcNow ?? DateTime.UtcNow, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone);

        var startLocal = DateTime.SpecifyKind(local.Date.AddHours(startHour), DateTimeKind.Unspecified);
        if (local < startLocal)
            startLocal = startLocal.AddDays(-1);

        // A start hour that falls in a spring-forward gap does not exist; use the first valid instant after it.
        if (zone.IsInvalidTime(startLocal))
            startLocal = startLocal.AddHours(1);

        return TimeZoneInfo.ConvertTimeToUtc(startLocal, zone);
    }

    /// <summary>
    /// Convenience overload using the configured zone and start hour.
    /// </summary>
    public static DateTime StartUtc(Microsoft.Extensions.Configuration.IConfiguration? config, DateTime? utcNow = null)
    {
        var (zone, hour) = Resolve(config);
        return StartUtc(zone, hour, utcNow);
    }

    /// <summary>
    /// Reads Restaurant:TimeZone (IANA id, default Europe/Copenhagen) and Restaurant:ServiceDayStartHour
    /// (0-23, default 5). Missing keys fall back to the defaults; invalid values throw.
    /// </summary>
    public static (TimeZoneInfo Zone, int StartHour) Resolve(Microsoft.Extensions.Configuration.IConfiguration? config)
    {
        var id = config?["Restaurant:TimeZone"];
        if (string.IsNullOrWhiteSpace(id)) id = DefaultTimeZoneId;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(id);

        var hour = DefaultStartHour;
        var hourText = config?["Restaurant:ServiceDayStartHour"];
        if (!string.IsNullOrWhiteSpace(hourText))
        {
            if (!int.TryParse(hourText, out hour) || hour < 0 || hour > 23)
                throw new InvalidOperationException(
                    $"Restaurant:ServiceDayStartHour must be an integer 0-23, got '{hourText}'.");
        }
        return (zone, hour);
    }
}
