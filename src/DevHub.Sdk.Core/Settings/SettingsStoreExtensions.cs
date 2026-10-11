namespace DevHub.Sdk.Core.Settings;

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

    /// <summary>A plugin's settings section, freshly loaded (defaults if never saved).</summary>
    /// <example><c>var git = store.LoadSection&lt;GitSettings&gt;();</c></example>
    public static T LoadSection<T>(this ISettingsStore store) where T : class, ISettingsSection, new()
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.Load().GetSection<T>();
    }

    /// <summary>Load → change a plugin's section → save, like <see cref="Update"/> (no lost updates).</summary>
    /// <example><c>store.UpdateSection&lt;GitSettings&gt;(s => s.ChangesAsTree = value);</c></example>
    public static void UpdateSection<T>(this ISettingsStore store, Action<T> change) where T : class, ISettingsSection, new()
    {
        ArgumentNullException.ThrowIfNull(change);
        store.Update(settings =>
        {
            var section = settings.GetSection<T>();
            change(section);
            settings.SetSection(section);
        });
    }
}
