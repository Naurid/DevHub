using DevHub.Core.Formatting;
using Xunit;

namespace DevHub.Core.Tests;

public class PathTreeTests
{
    /// <summary>The tree as text: one line per node, two spaces per level, folders end with "/".</summary>
    private static string Show<T>(IReadOnlyList<PathTreeNode<T>> nodes, int depth = 0) =>
        string.Concat(nodes.Select(n => new string(' ', depth * 2) + n + "\n" + Show(n.Children, depth + 1)));

    [Fact]
    public void Branch_names_become_folders_sorted_folders_first()
    {
        var tree = PathTree.Build(["main", "feature/search", "feature/login", "bugfix/x/deep", "Zeta"], b => b);

        Assert.Equal(
            "bugfix/\n" +
            "  x/\n" +
            "    deep\n" +
            "feature/\n" +
            "  login\n" +
            "  search\n" +
            "main\n" +
            "Zeta\n",
            Show(tree));
    }

    [Fact]
    public void Single_child_folder_chains_are_merged_when_compressing()
    {
        var tree = PathTree.Build(
            ["src/DevHub.App/Views/Main.axaml", "src/DevHub.App/App.cs", "docs/a.md", "README.md"], p => p, compress: true);

        Assert.Equal(
            "docs/\n" +
            "  a.md\n" +
            "src/DevHub.App/\n" +
            "  Views/\n" +
            "    Main.axaml\n" +
            "  App.cs\n" +
            "README.md\n",
            Show(tree));
        // The merged folder's key is its full path, and items keep their full path.
        Assert.Equal("src/DevHub.App", tree[1].Path);
        Assert.Equal("src/DevHub.App/Views/Main.axaml", tree[1].Children[0].Children[0].Path);
    }

    [Fact]
    public void An_untracked_folder_entry_stays_one_leaf()
    {
        var tree = PathTree.Build(["build/", "src/new/", "src/a.cs"], p => p, compress: true);

        // Real folders first; "build/" (git's single entry for an untracked folder) is a leaf among the files.
        Assert.Equal("src/\n  a.cs\n  new/\nbuild/\n", Show(tree));
        Assert.True(tree[0].IsFolder);
        Assert.False(tree[1].IsFolder);
        Assert.Equal("build/", tree[1].Path);
    }

    [Fact]
    public void Leaves_carry_their_items_and_flatten_visits_everything()
    {
        var tree = PathTree.Build([(Id: 1, Path: "a/b"), (Id: 2, Path: "c")], x => x.Path);

        var all = PathTree.Flatten(tree).ToList();

        Assert.Equal(["a", "b", "c"], all.Select(n => n.Name));
        Assert.Equal(1, all[1].Item.Id);
        Assert.Equal(2, all[2].Item.Id);
    }
}
