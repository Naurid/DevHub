using DevHub.Core.Git;
using Xunit;

namespace DevHub.Core.Tests.Git;

public class RepositoryWatcherTests
{
    [Theory]
    [InlineData("src/App.cs", true)]
    [InlineData("readme.md", true)]
    [InlineData(".gitignore", true)]
    [InlineData(".git/index", true)]
    [InlineData(".git/HEAD", true)]
    [InlineData(".git/refs/heads/main", true)]
    [InlineData(".git/MERGE_HEAD", true)]
    [InlineData(".git/rebase-merge/done", true)]
    [InlineData(@".git\packed-refs", true)]
    [InlineData(".git/objects/ab/cdef", false)]
    [InlineData(".git/logs/HEAD", false)]
    [InlineData(".git/index.lock", false)]
    [InlineData(".git", false)]
    public void Only_changes_that_affect_status_are_relevant(string relativePath, bool relevant)
    {
        Assert.Equal(relevant, RepositoryWatcher.IsRelevant(relativePath));
    }

    [Fact]
    public async Task A_burst_of_file_writes_raises_Changed_once()
    {
        var root = Path.Combine(Path.GetTempPath(), "devhub-watch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var watcher = new RepositoryWatcher(root, TimeSpan.FromMilliseconds(200));
            var count = 0;
            watcher.Changed += (_, _) => Interlocked.Increment(ref count);

            for (var i = 0; i < 20; i++)
                await File.WriteAllTextAsync(Path.Combine(root, $"f{i}.txt"), "x");

            // Wait well past the debounce.
            await Task.Delay(1500);

            Assert.Equal(1, count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
