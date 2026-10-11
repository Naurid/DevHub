using DevHub.Sdk.Core.Formatting;
using Xunit;

namespace DevHub.Sdk.Core.Tests;

public class RelativeTimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 15, 0, 0, TimeSpan.FromHours(2));

    [Theory]
    [InlineData(0, "just now")]
    [InlineData(-5, "just now")] // future (clock skew)
    [InlineData(60, "1 minute ago")]
    [InlineData(45 * 60, "45 minutes ago")]
    [InlineData(3600, "1 hour ago")]
    [InlineData(5 * 3600, "5 hours ago")]
    [InlineData(26 * 3600, "yesterday")]
    [InlineData(3 * 86400, "3 days ago")]
    [InlineData(40 * 86400, "Aug 31")]
    [InlineData(400 * 86400, "Sep 5, 2025")]
    public void Formats(int secondsAgo, string expected)
    {
        Assert.Equal(expected, RelativeTime.Format(Now.AddSeconds(-secondsAgo), Now));
    }
}
