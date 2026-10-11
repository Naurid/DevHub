namespace DevHub.Sdk.Core.Plugins;

/// <summary>
/// What a plugin is, for the host and the Plugins page: its id, name, description and the
/// plugins it can't work without. Plain data (no code), so <see cref="PluginResolver"/> can be
/// tested without loading anything.
/// </summary>
/// <param name="Id">Stable, lowercase, dotted id ("devhub.git"). Also the key of its settings section.</param>
/// <param name="Name">Shown in the Plugins page ("Git").</param>
/// <param name="Description">One or two sentences for the Plugins page.</param>
/// <param name="Requires">Ids of plugins this one needs (hard dependencies): they are switched on with it.</param>
public sealed record PluginInfo(string Id, string Name, string Description, IReadOnlyList<string> Requires)
{
    /// <summary>Grouping on the Plugins page ("Version control", "Templates", …).</summary>
    public string Category { get; init; } = "General";

    public string Version { get; init; } = "1.0";

    public override string ToString() => Id;
}
