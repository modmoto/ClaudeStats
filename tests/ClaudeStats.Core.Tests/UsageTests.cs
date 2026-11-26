using ClaudeStats.Core;
using Xunit;

namespace ClaudeStats.Core.Tests;

public class UsageTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Parses_unix_seconds()
    {
        var reset = Now.AddHours(2).ToUnixTimeSeconds();
        var json = $$"""
            {"rate_limits":{"five_hour":{"used_percentage":32,"resets_at":{{reset}}},
                            "seven_day":{"used_percentage":81.5,"resets_at":{{reset}}}}}
            """;

        var snapshot = UsageParser.Parse(json, Now);

        Assert.NotNull(snapshot);
        Assert.Equal(32, snapshot!.FiveHour!.UsedPercentage);
        Assert.Equal(81.5, snapshot.SevenDay!.UsedPercentage);
        Assert.Equal(Now.AddHours(2), snapshot.FiveHour.ResetsAt);
    }

    [Fact]
    public void Parses_iso_timestamp()
    {
        var json = """{"rate_limits":{"five_hour":{"used_percentage":10,"resets_at":"2026-11-26T14:00:00+00:00"}}}""";

        var snapshot = UsageParser.Parse(json, Now);

        Assert.Equal(Now.AddHours(2), snapshot!.FiveHour!.ResetsAt);
        Assert.Null(snapshot.SevenDay);
    }

    [Fact]
    public void Missing_rate_limits_gives_empty_snapshot()
    {
        var snapshot = UsageParser.Parse("""{"model":{"display_name":"x"}}""", Now);

        Assert.NotNull(snapshot);
        Assert.Null(snapshot!.FiveHour);
        Assert.Null(snapshot.SevenDay);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    public void Invalid_input_gives_null(string json) =>
        Assert.Null(UsageParser.Parse(json, Now));

    [Fact]
    public void Remaining_counts_down_from_used()
    {
        var window = new RateWindow(32, Now.AddHours(1));
        Assert.Equal(68, window.RemainingPercentage(Now));
    }

    [Fact]
    public void Remaining_is_clamped()
    {
        Assert.Equal(0, new RateWindow(130, Now.AddHours(1)).RemainingPercentage(Now));
        Assert.Equal(100, new RateWindow(-5, Now.AddHours(1)).RemainingPercentage(Now));
    }

    [Fact]
    public void Remaining_is_full_after_reset_time_passed()
    {
        var window = new RateWindow(95, Now.AddMinutes(-1));

        Assert.True(window.HasReset(Now));
        Assert.Equal(100, window.RemainingPercentage(Now));
        Assert.Null(window.TimeUntilReset(Now));
    }

    [Theory]
    [InlineData(null, UsageLevel.Unknown)]
    [InlineData(100.0, UsageLevel.Ok)]
    [InlineData(30.0, UsageLevel.Ok)]
    [InlineData(29.9, UsageLevel.Low)]
    [InlineData(10.0, UsageLevel.Low)]
    [InlineData(9.9, UsageLevel.Critical)]
    [InlineData(0.0, UsageLevel.Critical)]
    public void Level_thresholds(double? remaining, UsageLevel expected) =>
        Assert.Equal(expected, UsageLevels.For(remaining));

    [Theory]
    [InlineData(30, "30 Min")]
    [InlineData(134, "2 Std 14 Min")]
    public void Countdown_format_hours_and_minutes(int minutes, string expected) =>
        Assert.Equal(expected, UsageFormatter.Countdown(TimeSpan.FromMinutes(minutes)));

    [Fact]
    public void Countdown_format_days()
    {
        Assert.Equal("3 T 4 Std", UsageFormatter.Countdown(TimeSpan.FromHours(3 * 24 + 4)));
    }

    [Fact]
    public void Line_shows_remaining_and_countdown()
    {
        var line = UsageFormatter.Line("5-Stunden-Limit", new RateWindow(32, Now.AddHours(2).AddMinutes(14)), Now);
        Assert.Equal("5-Stunden-Limit: 68 % übrig · Reset in 2 Std 14 Min", line);
    }
}
