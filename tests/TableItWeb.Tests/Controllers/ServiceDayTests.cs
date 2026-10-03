using TableItWeb.Services;

namespace TableItWeb.Tests.Controllers;

public class ServiceDayTests
{
    private static DateTime Local(int year, int month, int day, int hour, int minute = 0, int second = 0) =>
        new(year, month, day, hour, minute, second, DateTimeKind.Local);

    // Expected values are computed from local DateTimes so the tests hold in any machine time zone.
    private static DateTime ExpectedUtc(int year, int month, int day) =>
        Local(year, month, day, 5).ToUniversalTime();

    [Fact]
    public void StartUtc_At2300Local_ReturnsTodayAt0500Local()
    {
        var result = ServiceDay.StartUtc(Local(2025, 6, 15, 23));

        Assert.Equal(ExpectedUtc(2025, 6, 15), result);
    }

    [Fact]
    public void StartUtc_At0300Local_ReturnsYesterdayAt0500Local()
    {
        var result = ServiceDay.StartUtc(Local(2025, 6, 15, 3));

        Assert.Equal(ExpectedUtc(2025, 6, 14), result);
    }

    [Fact]
    public void StartUtc_ExactlyAt0500Local_ReturnsToday()
    {
        var result = ServiceDay.StartUtc(Local(2025, 6, 15, 5));

        Assert.Equal(ExpectedUtc(2025, 6, 15), result);
    }

    [Fact]
    public void StartUtc_OneSecondBefore0500Local_ReturnsYesterday()
    {
        var result = ServiceDay.StartUtc(Local(2025, 6, 15, 4, 59, 59));

        Assert.Equal(ExpectedUtc(2025, 6, 14), result);
    }

    [Fact]
    public void StartUtc_JustAfterMidnight_ReturnsPreviousDayAcrossMonthBoundary()
    {
        var result = ServiceDay.StartUtc(Local(2025, 3, 1, 0, 30));

        Assert.Equal(ExpectedUtc(2025, 2, 28), result);
    }

    [Theory]
    [InlineData(23)]
    [InlineData(3)]
    [InlineData(5)]
    public void StartUtc_AnyTime_ReturnsKindUtcEqualToLocal0500ConvertedToUtc(int hour)
    {
        var now = Local(2025, 1, 10, hour);
        var expectedLocalStart = hour < 5 ? Local(2025, 1, 9, 5) : Local(2025, 1, 10, 5);

        var result = ServiceDay.StartUtc(now);

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(expectedLocalStart.ToUniversalTime(), result);
        Assert.Equal(5, result.ToLocalTime().Hour);
    }

    [Fact]
    public void StartUtc_NoArgument_UsesCurrentTimeAndReturnsPastUtcInstantWithinOneDay()
    {
        var result = ServiceDay.StartUtc();

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.True(result <= DateTime.UtcNow.AddMinutes(1));
        Assert.True(result > DateTime.UtcNow.AddHours(-30));
    }
}
