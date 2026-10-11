namespace DevHub.Sdk.Core.Settings;

/// <summary>
/// Loads and saves <see cref="AppSettings"/>. The seam lets ViewModels read/write settings
/// without knowing where (or how) they're persisted.
/// </summary>
public interface ISettingsStore
{
    /// <summary>Loads settings, returning defaults if nothing is saved yet.</summary>
    AppSettings Load();

    /// <summary>Persists settings, overwriting any previous save.</summary>
    void Save(AppSettings settings);
}
