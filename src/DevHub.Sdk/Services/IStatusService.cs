namespace DevHub.Services;

/// <summary>
/// The status bar (bottom): a short message on the left, like "Found 12 repositories.", and
/// the operation that's running (fetch, push…) with its progress and a Cancel button.
/// ViewModels call this instead of each page keeping its own status line.
/// </summary>
public interface IStatusService
{
    /// <summary>Replaces the current message. Null or empty clears it.</summary>
    void Show(string? message);

    /// <summary>
    /// Shows a running operation ("Pushing main to origin…") until the returned task is
    /// disposed. <paramref name="cancel"/> (optional) is what the Cancel button does.
    /// Call from the UI thread; <see cref="IStatusTask.Report"/> may be called from any thread.
    /// </summary>
    IStatusTask StartTask(string title, Action? cancel = null);
}

/// <summary>A running operation in the status bar. Dispose when it's done.</summary>
public interface IStatusTask : IDisposable
{
    /// <summary>The current step ("Receiving objects") and its percentage (null = no percentage, shows an indeterminate bar).</summary>
    void Report(string? step, double? percent);
}
