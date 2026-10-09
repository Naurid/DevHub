namespace DevHub.Services;

/// <summary>
/// Shows a short message in the status bar (bottom-left), like "Found 12 repositories."
/// ViewModels call this instead of each page keeping its own status line.
/// </summary>
public interface IStatusService
{
    /// <summary>Replaces the current message. Null or empty clears it.</summary>
    void Show(string? message);
}
