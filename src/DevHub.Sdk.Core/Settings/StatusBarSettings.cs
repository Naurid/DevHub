namespace DevHub.Sdk.Core.Settings;

/// <summary>Settings → Appearance: how many status-bar messages the history keeps.</summary>
public sealed class StatusBarSettings
{
    public const int DefaultHistorySize = 30, MinHistorySize = 5, MaxHistorySize = 200;

    /// <summary>Messages kept (newest first) for the history the status message opens on a click.</summary>
    public int HistorySize { get; set; } = DefaultHistorySize;

    /// <summary><see cref="HistorySize"/> within <see cref="MinHistorySize"/>–<see cref="MaxHistorySize"/> (a hand-edited file).</summary>
    public int Size => Math.Clamp(HistorySize, MinHistorySize, MaxHistorySize);
}
