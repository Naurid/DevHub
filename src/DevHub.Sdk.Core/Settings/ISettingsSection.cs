namespace DevHub.Sdk.Core.Settings;

/// <summary>
/// A plugin's settings: a plain class (public get/set properties, parameterless constructor)
/// stored as its own JSON object under <c>"plugins": { "&lt;SectionId&gt;": { … } }</c> in
/// settings.json. The shell never needs to know the type, so a plugin adds settings without
/// touching <see cref="AppSettings"/>. Read with <c>store.LoadSection&lt;T&gt;()</c>, change with
/// <c>store.UpdateSection&lt;T&gt;(s =&gt; …)</c>.
/// </summary>
public interface ISettingsSection
{
    /// <summary>The key in settings.json: the owning plugin's id, e.g. "devhub.git".</summary>
    static abstract string SectionId { get; }
}
