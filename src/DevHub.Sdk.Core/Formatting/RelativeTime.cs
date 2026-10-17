using System.Globalization;
using DevHub.Sdk.Core.Localization;

namespace DevHub.Sdk.Core.Formatting;

/// <summary>"just now", "5 minutes ago", "yesterday", "3 days ago", then a date: how lists show when something happened.</summary>
public static class RelativeTime
{
    public static string Format(DateTimeOffset when, DateTimeOffset now)
    {
        var age = now - when;
        if (age < TimeSpan.FromMinutes(1))
            return L.T("just now"); // also clock skew (a date in the future)
        if (age < TimeSpan.FromHours(1))
            return (int)age.TotalMinutes is var minutes && minutes == 1 ? L.T("1 minute ago") : L.T($"{minutes} minutes ago");
        if (age < TimeSpan.FromDays(1))
            return (int)age.TotalHours is var hours && hours == 1 ? L.T("1 hour ago") : L.T($"{hours} hours ago");

        var days = (now.Date - when.ToOffset(now.Offset).Date).Days;
        if (days <= 1)
            return L.T("yesterday");
        if (days < 30)
            return L.T($"{days} days ago");
        // "Oct 7" / "Oct 7, 2025" in English (whatever the region), "7 oct." / "7 oct. 2025" in French.
        var culture = L.IsFrench ? CultureInfo.CurrentCulture : CultureInfo.InvariantCulture;
        var format = L.IsFrench ? (when.Year == now.Year ? "d MMM" : "d MMM yyyy") : (when.Year == now.Year ? "MMM d" : "MMM d, yyyy");
        return when.ToOffset(now.Offset).ToString(format, culture);
    }
}
