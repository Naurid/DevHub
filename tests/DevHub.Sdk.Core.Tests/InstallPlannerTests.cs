using DevHub.Sdk.Core.Plugins.Marketplace;
using Xunit;

namespace DevHub.Sdk.Core.Tests;

public sealed class InstallPlannerTests
{
    private static IndexEntry Entry(string id, int api = PluginApi.Version, params string[] requires) =>
        new(id, id.ToUpperInvariant(), "1.0.0", null, null, api, requires, $"https://example.com/{id}.zip", new string('0', 64), 1, null);

    private static IndexEntry Entry(string id, params string[] requires) => Entry(id, PluginApi.Version, requires);

    private static PluginPackageInfo Installed(string id, params string[] requires) =>
        new() { Id = id, Name = id.ToUpperInvariant(), Version = "1.0.0", ApiVersion = PluginApi.Version, Requires = requires };

    private static string[] Ids(InstallPlan plan) => plan.Install.Select(e => e.Id).ToArray();

    [Fact]
    public void A_plugin_without_requirements_is_installed_alone()
    {
        var a = Entry("a");
        var plan = InstallPlanner.PlanInstall(a, [a], []);
        Assert.Null(plan.Problem);
        Assert.Equal(["a"], Ids(plan));
        Assert.Empty(plan.AlsoInstalled);
    }

    [Fact]
    public void Requirements_come_first_requirements_of_requirements_before_them()
    {
        // github → git → shell-tools
        var github = Entry("github", "git");
        var git = Entry("git", "shell-tools");
        var tools = Entry("shell-tools");
        var plan = InstallPlanner.PlanInstall(github, [github, git, tools], []);
        Assert.Null(plan.Problem);
        Assert.Equal(["shell-tools", "git", "github"], Ids(plan));
        Assert.Equal(["shell-tools", "git"], plan.AlsoInstalled.Select(e => e.Id));
    }

    [Fact]
    public void A_shared_requirement_is_installed_once()
    {
        // board needs git and ci; ci needs git too
        var board = Entry("board", "git", "ci");
        var ci = Entry("ci", "git");
        var git = Entry("git");
        var plan = InstallPlanner.PlanInstall(board, [board, ci, git], []);
        Assert.Null(plan.Problem);
        Assert.Equal(["git", "ci", "board"], Ids(plan));
    }

    [Fact]
    public void Requirements_already_there_are_not_installed_again()
    {
        var github = Entry("github", "git", "projects");
        var git = Entry("git");
        var projects = Entry("projects");
        // git built in or installed (any case), projects waiting for the restart
        var plan = InstallPlanner.PlanInstall(github, [github, git, projects], ["GIT", "projects"]);
        Assert.Null(plan.Problem);
        Assert.Equal(["github"], Ids(plan));
    }

    [Fact]
    public void An_installed_plugin_is_still_planned_when_it_is_the_one_being_updated()
    {
        var git = Entry("git");
        var plan = InstallPlanner.PlanInstall(git, [git], ["git"]);
        Assert.Equal(["git"], Ids(plan));
    }

    [Fact]
    public void An_update_installs_a_requirement_its_new_version_added()
    {
        var github = Entry("github", "git");
        var git = Entry("git");
        var plan = InstallPlanner.PlanInstall(github, [github, git], ["github"]);
        Assert.Equal(["git", "github"], Ids(plan));
    }

    [Fact]
    public void A_requirement_no_source_offers_stops_the_install()
    {
        var github = Entry("github", "git");
        var plan = InstallPlanner.PlanInstall(github, [github], []);
        Assert.Empty(plan.Install);
        Assert.Contains("\"git\"", plan.Problem);
        Assert.Contains("no marketplace source offers", plan.Problem);
    }

    [Fact]
    public void A_missing_requirement_deeper_down_names_the_plugin_that_needs_it()
    {
        var github = Entry("github", "git");
        var git = Entry("git", "shell-tools");
        var plan = InstallPlanner.PlanInstall(github, [github, git], []);
        Assert.Empty(plan.Install);
        Assert.StartsWith("GIT requires \"shell-tools\"", plan.Problem);
    }

    [Fact]
    public void A_requirement_for_another_plugin_api_stops_the_install()
    {
        var github = Entry("github", "git");
        var git = Entry("git", PluginApi.Version + 1);
        var plan = InstallPlanner.PlanInstall(github, [github, git], []);
        Assert.Empty(plan.Install);
        Assert.Contains($"plugin API {PluginApi.Version + 1}", plan.Problem);
    }

    [Fact]
    public void A_plugin_for_another_plugin_api_is_refused()
    {
        var a = Entry("a", PluginApi.Version + 1);
        var plan = InstallPlanner.PlanInstall(a, [a], []);
        Assert.Empty(plan.Install);
        Assert.NotNull(plan.Problem);
    }

    [Fact]
    public void Requirements_in_a_circle_stop_the_install_and_name_the_circle()
    {
        var a = Entry("a", "b");
        var b = Entry("b", "c");
        var c = Entry("c", "b");
        var plan = InstallPlanner.PlanInstall(a, [a, b, c], []);
        Assert.Empty(plan.Install);
        Assert.Contains("B → C → B", plan.Problem);
    }

    [Fact]
    public void A_plugin_requiring_itself_is_a_circle()
    {
        var a = Entry("a", "a");
        var plan = InstallPlanner.PlanInstall(a, [a], []);
        Assert.Empty(plan.Install);
        Assert.Contains("circle", plan.Problem);
    }

    [Fact]
    public void Removing_a_plugin_names_the_ones_that_need_it_directly_or_not()
    {
        var installed = new[]
        {
            Installed("git"), Installed("github", "git"), Installed("board-git", "github"),
            Installed("projects"), Installed("templates", "projects"),
        };
        var dependents = InstallPlanner.Dependents("git", installed);
        Assert.Equal(["github", "board-git"], dependents.Select(p => p.Id));
    }

    [Fact]
    public void Removing_a_plugin_nothing_needs_names_nobody()
    {
        var installed = new[] { Installed("git"), Installed("github", "git") };
        Assert.Empty(InstallPlanner.Dependents("github", installed));
        Assert.Empty(InstallPlanner.Dependents("not-installed", installed));
    }

    [Fact]
    public void Dependents_compare_ids_without_case_and_survive_a_circle()
    {
        var installed = new[] { Installed("Git"), Installed("a", "GIT", "b"), Installed("b", "a") };
        Assert.Equal(["a", "b"], InstallPlanner.Dependents("git", installed).Select(p => p.Id));
    }
}
