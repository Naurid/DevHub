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

    /// <summary>
    /// Where the plugin comes in lists: discovery (and so load) order among unrelated plugins, the
    /// main-menu order of its groups, the Plugins page. Lowest first; requirements still load first.
    /// </summary>
    public int Order { get; init; } = 1000;

    /// <summary>
    /// The first-run presets this plugin belongs to ("Git client", "Project templates"). The
    /// picker offers every preset any plugin names, plus "Everything"; choosing one switches on
    /// exactly its plugins (and their requirements). Lets the shell offer presets without knowing
    /// any plugin.
    /// </summary>
    public IReadOnlyList<string> Presets { get; init; } = [];

    public override string ToString() => Id;
}
