using DevHub.Sdk.Core.Collections;
using DevHub.Sdk.Core.Settings;
using Xunit;

namespace DevHub.Sdk.Core.Tests;

public class StatusHistoryTests
{
    [Fact]
    public void Recent_items_keep_the_newest_first_up_to_their_capacity()
    {
        var recent = new RecentItems<string>(3);
        foreach (var message in new[] { "a", "b", "c", "d" })
            recent.Push(message);

        Assert.Equal(["d", "c", "b"], recent);
    }

    [Fact]
    public void Lowering_the_capacity_drops_the_oldest_and_raising_it_keeps_more()
    {
        var recent = new RecentItems<int>(5);
        for (var i = 1; i <= 5; i++)
            recent.Push(i);

        recent.Capacity = 2;
        Assert.Equal([5, 4], recent);

        recent.Capacity = 4;
        recent.Push(6);
        recent.Push(7);
        Assert.Equal([7, 6, 5, 4], recent);
    }

    [Fact]
    public void The_history_size_is_saved_with_the_shell_settings_and_defaults_to_30()
    {
        var dir = Directory.CreateTempSubdirectory("devhub-status-");
        try
        {
            var store = new JsonSettingsStore(dir.FullName);
            Assert.Null(store.Load().StatusBar);
            Assert.Equal(30, new StatusBarSettings().Size);

            store.Update(s => s.StatusBar = new StatusBarSettings { HistorySize = 12 });
            Assert.Equal(12, store.Load().StatusBar!.Size);
        }
        finally
        {
            dir.Delete(recursive: true);
        }

        // A hand-edited file can't make it absurd.
        Assert.Equal(StatusBarSettings.MaxHistorySize, new StatusBarSettings { HistorySize = 100000 }.Size);
        Assert.Equal(StatusBarSettings.MinHistorySize, new StatusBarSettings { HistorySize = 0 }.Size);
    }
}
