namespace DevHub.Core.Formatting;

/// <summary>
/// A folder or an item in a <see cref="PathTree"/>: "feature/login" becomes a folder "feature"
/// holding the item "login"; "src/App/View.axaml" becomes folders "src", "App" and the item.
/// </summary>
/// <typeparam name="T">What the leaves carry (a branch, a changed file).</typeparam>
public sealed class PathTreeNode<T>
{
    /// <summary>What's shown: the folder's name ("feature", or "src/DevHub.App" when compressed) or the item's last part.</summary>
    public required string Name { get; init; }

    /// <summary>The full path up to and including this node ("src/DevHub.App"); a stable key for remembering what's folded.</summary>
    public required string Path { get; init; }

    /// <summary>The item, for a leaf; default for a folder.</summary>
    public T? Item { get; init; }

    public bool IsFolder { get; init; }

    /// <summary>Folders first, then items, each sorted by name (case-insensitive).</summary>
    public IReadOnlyList<PathTreeNode<T>> Children { get; init; } = [];

    public override string ToString() => IsFolder ? Name + "/" : Name;
}

/// <summary>
/// Turns "/"-separated paths into folders (Fork's branch folders, its tree view of changed files).
/// Pure and tested; the views only show the result.
/// </summary>
public static class PathTree
{
    /// <param name="items">The leaves.</param>
    /// <param name="path">Each item's path ("feature/login"). A trailing "/" (an untracked folder) is part of its name.</param>
    /// <param name="compress">
    /// Merge chains of folders that hold nothing but one subfolder ("src" → "DevHub.App" shown
    /// as "src/DevHub.App"), like Fork's file tree. Branch folders aren't compressed.
    /// </param>
    public static IReadOnlyList<PathTreeNode<T>> Build<T>(IEnumerable<T> items, Func<T, string> path, bool compress = false)
    {
        var root = new Folder<T>("", "");
        foreach (var item in items)
        {
            var full = path(item);
            // Split on "/" but keep a trailing one with the last part ("newdir/" is a leaf, not a folder).
            var trimmed = full.TrimEnd('/');
            var parts = trimmed.Split('/');
            if (full.EndsWith('/'))
                parts[^1] += "/";

            var folder = root;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                var name = parts[i];
                if (!folder.Folders.TryGetValue(name, out var next))
                    folder.Folders[name] = next = new Folder<T>(name, folder.Path.Length == 0 ? name : folder.Path + "/" + name);
                folder = next;
            }
            folder.Items.Add((parts[^1], full, item));
        }
        return Convert(root, compress);
    }

    private static List<PathTreeNode<T>> Convert<T>(Folder<T> folder, bool compress)
    {
        var nodes = new List<PathTreeNode<T>>();
        foreach (var sub in folder.Folders.Values.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
        {
            var current = sub;
            var name = sub.Name;
            // "src" with only "DevHub.App" in it (and no files) → one node "src/DevHub.App".
            while (compress && current.Items.Count == 0 && current.Folders.Count == 1)
            {
                current = current.Folders.Values.Single();
                name += "/" + current.Name;
            }
            nodes.Add(new PathTreeNode<T> { Name = name, Path = current.Path, IsFolder = true, Children = Convert(current, compress) });
        }
        nodes.AddRange(folder.Items
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Select(i => new PathTreeNode<T> { Name = i.Name, Path = i.Path, Item = i.Item }));
        return nodes;
    }

    /// <summary>Visits every node depth-first (folders before their contents).</summary>
    public static IEnumerable<PathTreeNode<T>> Flatten<T>(IEnumerable<PathTreeNode<T>> nodes) =>
        nodes.SelectMany(n => new[] { n }.Concat(Flatten(n.Children)));

    private sealed class Folder<T>(string name, string path)
    {
        public string Name { get; } = name;
        public string Path { get; } = path;
        public Dictionary<string, Folder<T>> Folders { get; } = new(StringComparer.Ordinal);
        public List<(string Name, string Path, T Item)> Items { get; } = new();
    }
}
