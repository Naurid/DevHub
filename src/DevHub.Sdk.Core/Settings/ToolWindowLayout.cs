namespace DevHub.Sdk.Core.Settings;

/// <summary>
/// Which tool window is open in each slot of the main window, and how big each slot is.
/// Saved in <see cref="AppSettings.ToolWindows"/> so the window looks the same next launch.
/// Tool windows are referred to by id (e.g. "repositories"); null means the slot is closed.
/// </summary>
/// <remarks>
/// Slots are fixed for now: every tool window has one home slot (left, right or bottom) and
/// can't be moved, so only "what's open" and sizes need saving. Mutable with a parameterless
/// constructor so System.Text.Json round-trips it plainly, like the rest of AppSettings.
/// </remarks>
public sealed class ToolWindowLayout
{
    public const double DefaultSideWidth = 280;
    public const double DefaultBottomHeight = 220;

    /// <summary>Id of the tool window open on the left, or null.</summary>
    public string? OpenLeft { get; set; }

    /// <summary>Id of the tool window open on the right, or null.</summary>
    public string? OpenRight { get; set; }

    /// <summary>Id of the tool window open at the bottom, or null.</summary>
    public string? OpenBottom { get; set; }

    public double LeftWidth { get; set; } = DefaultSideWidth;
    public double RightWidth { get; set; } = DefaultSideWidth;
    public double BottomHeight { get; set; } = DefaultBottomHeight;
}
