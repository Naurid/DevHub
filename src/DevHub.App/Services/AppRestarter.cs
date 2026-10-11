using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace DevHub.Services;

/// <summary>Restarts DevHub, e.g. to load a new plugin choice (plugins are only loaded at startup).</summary>
public interface IAppRestarter
{
    void Restart();
}

/// <summary>
/// Starts a new DevHub process (same executable; it inherits DEVHUB_DATA_DIR and the rest of
/// the environment), then shuts this one down normally, so the open tabs and tool-window layout
/// are saved first and restored by the new one.
/// </summary>
public sealed class AppRestarter(IStatusService status) : IAppRestarter
{
    public void Restart()
    {
        if (Environment.ProcessPath is not { } exe)
        {
            status.Show("Couldn't find DevHub's executable to restart it; please start it again yourself.");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            status.Show("Couldn't restart DevHub: " + ex.Message);
            return;
        }
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }
}
