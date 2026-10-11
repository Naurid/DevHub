using DevHub.Sdk.Core.Plugins;
using Xunit;

namespace DevHub.Sdk.Core.Tests;

public class PluginSelectionTests
{
    private static readonly PluginInfo Projects = new("projects", "Projects", "", []) { Order = 1, Presets = ["Git client", "Templates"] };
    private static readonly PluginInfo Git = new("git", "Git", "", []) { Order = 2, Presets = ["Git client"] };
    private static readonly PluginInfo GitHub = new("github", "GitHub", "", ["git"]) { Order = 3, Presets = ["Git client"] };
    private static readonly PluginInfo Templates = new("templates", "Templates", "", ["projects"]) { Order = 4, Presets = ["Templates"] };
    private static readonly PluginInfo[] All = [Projects, Git, GitHub, Templates];

    private static string[] Ids(IEnumerable<PluginInfo> plugins) => plugins.Select(p => p.Id).ToArray();

    [Fact]
    public void Switching_on_a_plugin_switches_on_its_requirements()
    {
        var selection = new PluginSelection(All, []);

        var also = selection.Enable("github");

        Assert.Equal(["git", "github"], selection.Enabled);
        Assert.Equal(["git"], Ids(also));
    }

    [Fact]
    public void Switching_off_a_plugin_switches_off_what_needs_it()
    {
        var selection = new PluginSelection(All, ["projects", "git", "github", "templates"]);

        var also = selection.Disable("git");

        Assert.Equal(["projects", "templates"], selection.Enabled);
        Assert.Equal(["github"], Ids(also));
    }

    [Fact]
    public void Switching_off_something_nothing_needs_touches_nothing_else()
    {
        var selection = new PluginSelection(All, ["projects", "git"]);

        Assert.Empty(selection.Disable("git"));
        Assert.Equal(["projects"], selection.Enabled);
    }

    [Fact]
    public void Presets_come_from_the_plugins_in_order_then_everything()
    {
        Assert.Equal(["Git client", "Templates", PluginSelection.Everything], new PluginSelection(All, []).Presets);
    }

    [Fact]
    public void A_preset_switches_on_exactly_its_plugins_and_is_recognised()
    {
        var selection = new PluginSelection(All, ["templates", "projects"]);

        selection.ApplyPreset("Git client");

        Assert.Equal(["projects", "git", "github"], selection.Enabled);
        Assert.Equal("Git client", selection.MatchingPreset);
        selection.Disable("github");
        Assert.Null(selection.MatchingPreset);
        selection.ApplyPreset(PluginSelection.Everything);
        Assert.Equal(["projects", "git", "github", "templates"], selection.Enabled);
        Assert.Equal(PluginSelection.Everything, selection.MatchingPreset);
    }

    [Fact]
    public void Ids_of_plugins_that_are_not_installed_are_ignored()
    {
        Assert.Equal(["git"], new PluginSelection(All, ["git", "someone.else"]).Enabled);
    }
}
