namespace TableItWeb.Services;

public static class ServiceDay
{
    /// <summary>
    /// Start of the current service day in UTC: today 05:00 local, or yesterday 05:00 local if it is before 05:00.
    /// </summary>
    public static DateTime StartUtc(DateTime? nowLocal = null)
    {
        var now = nowLocal ?? DateTime.Now;
        var start = DateTime.SpecifyKind(now.Date.AddHours(5), DateTimeKind.Local);
        if (now.TimeOfDay < TimeSpan.FromHours(5))
            start = start.AddDays(-1);
        return start.ToUniversalTime();
    }
}
