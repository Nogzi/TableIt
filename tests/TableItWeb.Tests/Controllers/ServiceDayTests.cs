using Microsoft.Extensions.Configuration;
using TableItWeb.Services;

namespace TableItWeb.Tests.Controllers;

public class ServiceDayTests
{
    private static readonly TimeZoneInfo Cph = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    private static DateTime Utc(int y, int m, int d, int h, int min = 0, int s = 0) =>
        new(y, m, d, h, min, s, DateTimeKind.Utc);

    [Fact]
    public void StartUtc_SummerEvening_ReturnsTodayAt0500Local()
    {
        // 23:00 CEST (UTC+2) on 15 June -> start is 05:00 CEST = 03:00 UTC same day.
        var result = ServiceDay.StartUtc(Cph, 5, Utc(2025, 6, 15, 21));

        Assert.Equal(Utc(2025, 6, 15, 3), result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void StartUtc_WinterEvening_UsesWinterOffset()
    {
        // 23:00 CET (UTC+1) -> 05:00 CET = 04:00 UTC.
        var result = ServiceDay.StartUtc(Cph, 5, Utc(2025, 1, 10, 22));

        Assert.Equal(Utc(2025, 1, 10, 4), result);
    }

    [Fact]
    public void StartUtc_At0300Local_ReturnsYesterday()
    {
        // 03:00 CEST on 15 June = 01:00 UTC.
        var result = ServiceDay.StartUtc(Cph, 5, Utc(2025, 6, 15, 1));

        Assert.Equal(Utc(2025, 6, 14, 3), result);
    }

    [Fact]
    public void StartUtc_ExactlyAtStartHour_ReturnsToday()
    {
        var result = ServiceDay.StartUtc(Cph, 5, Utc(2025, 6, 15, 3));

        Assert.Equal(Utc(2025, 6, 15, 3), result);
    }

    [Fact]
    public void StartUtc_OneSecondBeforeStartHour_ReturnsYesterday()
    {
        var result = ServiceDay.StartUtc(Cph, 5, Utc(2025, 6, 15, 2, 59, 59));

        Assert.Equal(Utc(2025, 6, 14, 3), result);
    }

    [Fact]
    public void StartUtc_CustomStartHour_IsRespected()
    {
        // 06:30 CEST with start hour 7 -> still yesterday's service day (07:00 CEST = 05:00 UTC).
        var result = ServiceDay.StartUtc(Cph, 7, Utc(2025, 6, 15, 4, 30));

        Assert.Equal(Utc(2025, 6, 14, 5), result);
    }

    [Fact]
    public void StartUtc_SpringForwardDay_EarlyMorningBelongsToPreviousDayAtWinterOffset()
    {
        // 30 March 2025: clocks 02:00 -> 03:00 CET->CEST. 04:00 CEST = 02:00 UTC, before the 05:00 start.
        var result = ServiceDay.StartUtc(Cph, 5, Utc(2025, 3, 30, 2));

        Assert.Equal(Utc(2025, 3, 29, 4), result); // 05:00 CET the day before
    }

    [Fact]
    public void StartUtc_SpringForwardDay_AfterStartUsesSummerOffset()
    {
        // 12:00 CEST on 30 March = 10:00 UTC; start 05:00 CEST = 03:00 UTC.
        var result = ServiceDay.StartUtc(Cph, 5, Utc(2025, 3, 30, 10));

        Assert.Equal(Utc(2025, 3, 30, 3), result);
    }

    [Fact]
    public void StartUtc_DayAfterSpringForward_SpansOnly23Hours()
    {
        // 31 March 03:00 UTC = 05:00 CEST; previous start was 29 March 04:00 UTC (CET).
        var result = ServiceDay.StartUtc(Cph, 5, Utc(2025, 3, 31, 3));

        Assert.Equal(Utc(2025, 3, 31, 3), result);
    }

    [Fact]
    public void StartUtc_FallBackDay_EarlyMorningBelongsToPreviousDay()
    {
        // 26 Oct 2025: clocks 03:00 CEST -> 02:00 CET. 01:30 UTC = 02:30 CET (second pass), before 05:00.
        var result = ServiceDay.StartUtc(Cph, 5, Utc(2025, 10, 26, 1, 30));

        Assert.Equal(Utc(2025, 10, 25, 3), result); // 05:00 CEST the day before
    }

    [Fact]
    public void StartUtc_FallBackDay_AfterStartUsesWinterOffset()
    {
        // 12:00 CET = 11:00 UTC; start 05:00 CET = 04:00 UTC.
        var result = ServiceDay.StartUtc(Cph, 5, Utc(2025, 10, 26, 11));

        Assert.Equal(Utc(2025, 10, 26, 4), result);
    }

    [Fact]
    public void StartUtc_StartHourInSpringForwardGap_UsesFirstValidInstantAfterGap()
    {
        // Start hour 2 does not exist on 30 March 2025 in Copenhagen; 03:00 CEST = 01:00 UTC.
        var result = ServiceDay.StartUtc(Cph, 2, Utc(2025, 3, 30, 12));

        Assert.Equal(Utc(2025, 3, 30, 1), result);
    }

    [Fact]
    public void StartUtc_UtcZone_BehavesAsPlainUtc()
    {
        // A UTC host/zone must not shift the service day.
        Assert.Equal(Utc(2025, 6, 15, 5), ServiceDay.StartUtc(TimeZoneInfo.Utc, 5, Utc(2025, 6, 15, 23)));
        Assert.Equal(Utc(2025, 6, 14, 5), ServiceDay.StartUtc(TimeZoneInfo.Utc, 5, Utc(2025, 6, 15, 3)));
    }

    [Fact]
    public void StartUtc_ResultIndependentOfHostTimeZone()
    {
        // 23:30 UTC on 15 June is 01:30 CEST on 16 June -> still the 15th's service day.
        var now = Utc(2025, 6, 15, 23, 30);
        var original = TimeZoneInfo.Local;

        var result = ServiceDay.StartUtc(Cph, 5, now);

        Assert.Equal(Utc(2025, 6, 15, 3), result);
        Assert.Equal(original, TimeZoneInfo.Local);
    }

    [Fact]
    public void StartUtc_NoNowArgument_ReturnsPastUtcInstantWithinOneDay()
    {
        var result = ServiceDay.StartUtc(Cph, 5);

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.True(result <= DateTime.UtcNow.AddMinutes(1));
        Assert.True(result > DateTime.UtcNow.AddHours(-26));
    }

    private static IConfiguration Config(params (string, string?)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(
            values.Select(v => new KeyValuePair<string, string?>(v.Item1, v.Item2))).Build();

    [Fact]
    public void Resolve_MissingKeys_UsesDefaults()
    {
        var (zone, hour) = ServiceDay.Resolve(Config());

        Assert.Equal("Europe/Copenhagen", zone.Id);
        Assert.Equal(5, hour);
    }

    [Fact]
    public void Resolve_NullConfig_UsesDefaults()
    {
        var (zone, hour) = ServiceDay.Resolve(null);

        Assert.Equal("Europe/Copenhagen", zone.Id);
        Assert.Equal(5, hour);
    }

    [Fact]
    public void Resolve_ConfiguredValues_AreUsed()
    {
        var (zone, hour) = ServiceDay.Resolve(Config(
            ("Restaurant:TimeZone", "America/New_York"), ("Restaurant:ServiceDayStartHour", "6")));

        Assert.Equal("America/New_York", zone.Id);
        Assert.Equal(6, hour);
    }

    [Theory]
    [InlineData("24")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void Resolve_InvalidHour_Throws(string value)
    {
        Assert.Throws<InvalidOperationException>(() =>
            ServiceDay.Resolve(Config(("Restaurant:ServiceDayStartHour", value))));
    }

    [Fact]
    public void StartUtc_FromConfiguration_MatchesExplicitZone()
    {
        var now = Utc(2025, 6, 15, 21);

        Assert.Equal(ServiceDay.StartUtc(Cph, 5, now), ServiceDay.StartUtc(Config(), now));
    }
}
