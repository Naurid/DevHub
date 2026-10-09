using System.Globalization;

namespace DevHub.Core.Formatting;

/// <summary>"just now", "5 minutes ago", "yesterday", "3 days ago", then a date: how lists show when something happened.</summary>
public static class RelativeTime
{
    public static string Format(DateTimeOffset when, DateTimeOffset now)
    {
        var age = now - when;
        if (age < TimeSpan.FromMinutes(1))
            return "just now"; // also clock skew (a date in the future)
        if (age < TimeSpan.FromHours(1))
            return Plural((int)age.TotalMinutes, "minute");
        if (age < TimeSpan.FromDays(1))
            return Plural((int)age.TotalHours, "hour");

        var days = (now.Date - when.ToOffset(now.Offset).Date).Days;
        if (days <= 1)
            return "yesterday";
        if (days < 30)
            return Plural(days, "day");
        return when.ToOffset(now.Offset).ToString(when.Year == now.Year ? "MMM d" : "MMM d, yyyy", CultureInfo.InvariantCulture);
    }

    private static string Plural(int n, string unit) => n == 1 ? $"1 {unit} ago" : $"{n} {unit}s ago";
}
