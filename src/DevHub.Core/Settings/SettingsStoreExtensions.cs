namespace DevHub.Core.Settings;

public static class SettingsStoreExtensions
{
    /// <summary>
    /// Load → change one part → save. Use this instead of keeping an <see cref="AppSettings"/>
    /// object around and saving it later: several features write different sections of the
    /// same file, and saving a copy loaded earlier would overwrite whatever the others saved
    /// in the meantime (a "lost update").
    /// </summary>
    /// <example><c>store.Update(s => s.ToolWindows = layout);</c></example>
    public static void Update(this ISettingsStore store, Action<AppSettings> change)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(change);

        var settings = store.Load();
        change(settings);
        store.Save(settings);
    }
}
