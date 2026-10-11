using DevHub.Sdk.Core.Plugins;
using Xunit;

namespace DevHub.Sdk.Core.Tests;

public class PluginResolverTests
{
    private static PluginInfo P(string id, params string[] requires) => new(id, id.ToUpperInvariant(), "", requires);

    private static readonly PluginInfo Projects = P("projects");
    private static readonly PluginInfo Git = P("git", "projects");
    private static readonly PluginInfo GitHub = P("github", "git");
    private static readonly PluginInfo Templates = P("templates", "projects");
    private static readonly PluginInfo[] All = [Projects, Git, GitHub, Templates];

    private static string[] Ids(IEnumerable<PluginInfo> plugins) => plugins.Select(p => p.Id).ToArray();

    [Fact]
    public void With_no_choice_yet_everything_loads_in_discovery_order()
    {
        var result = PluginResolver.Resolve(All, enabled: null);

        Assert.Equal(["projects", "git", "github", "templates"], Ids(result.Loaded));
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void Only_switched_on_plugins_load()
    {
        var result = PluginResolver.Resolve(All, ["projects", "templates"]);

        Assert.Equal(["projects", "templates"], Ids(result.Loaded));
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void Requirements_are_switched_on_too()
    {
        var result = PluginResolver.Resolve(All, ["github"]);

        Assert.Equal(["projects", "git", "github"], Ids(result.Loaded));
    }

    [Fact]
    public void Requirements_load_before_the_plugins_that_need_them_whatever_the_discovery_order()
    {
        var result = PluginResolver.Resolve([GitHub, Templates, Git, Projects], enabled: null);

        Assert.Equal(4, result.Loaded.Count);
        var loaded = Ids(result.Loaded).ToList();
        Assert.True(loaded.IndexOf("projects") < loaded.IndexOf("git"));
        Assert.True(loaded.IndexOf("git") < loaded.IndexOf("github"));
        Assert.True(loaded.IndexOf("projects") < loaded.IndexOf("templates"));
    }

    [Fact]
    public void A_missing_requirement_skips_the_plugin_and_everything_that_needs_it()
    {
        var result = PluginResolver.Resolve([Git, GitHub, Templates], enabled: null); // no "projects" installed

        Assert.Empty(result.Loaded);
        Assert.Equal(["git", "github", "templates"], Ids(result.Skipped.Select(s => s.Plugin)));
        Assert.Contains("isn't installed", result.Skipped.Single(s => s.Plugin.Id == "git").Reason);
        Assert.Contains("GIT", result.Skipped.Single(s => s.Plugin.Id == "github").Reason);
    }

    [Fact]
    public void Plugins_requiring_each_other_in_a_circle_are_skipped()
    {
        var a = P("a", "b");
        var b = P("b", "a");

        var result = PluginResolver.Resolve([Projects, a, b], enabled: null);

        Assert.Equal(["projects"], Ids(result.Loaded));
        Assert.Equal(["a", "b"], Ids(result.Skipped.Select(s => s.Plugin)));
        Assert.All(result.Skipped, s => Assert.Contains("circle", s.Reason));
    }

    [Fact]
    public void A_duplicate_id_keeps_the_first_plugin()
    {
        var impostor = new PluginInfo("git", "Other Git", "", []);

        var result = PluginResolver.Resolve([Projects, Git, impostor], enabled: null);

        Assert.Same(Git, result.Loaded.Single(p => p.Id == "git"));
        Assert.Same(impostor, Assert.Single(result.Skipped).Plugin);
    }

    [Fact]
    public void Switched_on_ids_that_are_not_installed_are_ignored()
    {
        var result = PluginResolver.Resolve(All, ["projects", "someone.elses"]);

        Assert.Equal(["projects"], Ids(result.Loaded));
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void Nothing_switched_on_loads_nothing()
    {
        var result = PluginResolver.Resolve(All, []);

        Assert.Empty(result.Loaded);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void Dependents_are_what_stops_working_when_a_plugin_is_switched_off()
    {
        Assert.Equal(["git", "github", "templates"], Ids(PluginResolver.Dependents(All, "projects")));
        Assert.Equal(["github"], Ids(PluginResolver.Dependents(All, "git")));
        Assert.Empty(PluginResolver.Dependents(All, "github"));
    }
}
