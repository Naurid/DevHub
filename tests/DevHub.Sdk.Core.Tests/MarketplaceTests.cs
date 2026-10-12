using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevHub.Sdk.Core.Plugins.Marketplace;
using Xunit;

namespace DevHub.Sdk.Core.Tests;

public sealed class MarketplaceTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("devhub-market-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private string Root => Path.Combine(_dir, "plugins");

    /// <summary>A plugin package: plugin.json + the given files (name → text).</summary>
    private string Package(string id, string version, int api = PluginApi.Version, params (string Name, string Text)[] files)
    {
        var zip = Path.Combine(_dir, $"{id}-{version}-{Guid.NewGuid():N}.zip");
        using var archive = ZipFile.Open(zip, ZipArchiveMode.Create);
        Add(archive, PluginPackageInfo.FileName, JsonSerializer.Serialize(new { id, name = id, version, apiVersion = api }));
        foreach (var (name, text) in files)
            Add(archive, name, text);
        return zip;
    }

    private static void Add(ZipArchive archive, string name, string text)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(text);
    }

    private static string Sha(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));

    private static IndexEntry Entry(string id, string sha, string version = "1.0.0", int api = PluginApi.Version) =>
        new(id, id, version, null, null, api, null, $"https://example.com/{id}.zip", sha, 1, null);

    // ----- Index -----

    [Fact]
    public void ParseIndex_LeavesOutEntriesThatCantBeInstalledSafely()
    {
        var json = """
            [
              { "id": "good", "name": "Good", "version": "1.0.0", "apiVersion": 1, "url": "https://x/good.zip", "sha256": "%SHA%", "size": 1 },
              { "id": "http", "name": "Http", "version": "1.0.0", "apiVersion": 1, "url": "http://x/a.zip", "sha256": "%SHA%", "size": 1 },
              { "id": "nosha", "name": "NoSha", "version": "1.0.0", "apiVersion": 1, "url": "https://x/a.zip", "size": 1 },
              { "name": "NoId", "version": "1.0.0", "apiVersion": 1, "url": "https://x/a.zip", "sha256": "%SHA%", "size": 1 }, // comments are fine
            ]
            """.Replace("%SHA%", new string('a', 64));

        Assert.Equal(["good"], Marketplace.ParseIndex(json).Select(e => e.Id));
    }

    [Fact]
    public async Task LoadAsync_FirstSourceWins_ABrokenSourceIsReported()
    {
        static string Index(params (string Id, string Name)[] entries) =>
            JsonSerializer.Serialize(entries.Select(e => new { id = e.Id, name = e.Name, version = "1", apiVersion = 1, url = $"https://x/{e.Id}.zip", sha256 = new string('b', 64), size = 1 }));
        var first = Path.Combine(_dir, "first.json");
        File.WriteAllText(first, Index(("x", "From first")));
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Index(("x", "From web"), ("y", "Y"))),
        });

        var listing = await Marketplace.LoadAsync(new HttpClient(handler), [first, "https://example.com/index.json", Path.Combine(_dir, "missing.json")]);

        Assert.Equal(["From first", "Y"], listing.Entries.Select(e => e.Name));
        Assert.Single(listing.Problems);
    }

    [Theory]
    [InlineData("1.10.0", "1.9.2", true)]
    [InlineData("2", "1.9", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("0.9.0", "1.0.0", false)]
    public void IsNewer_ComparesNumbers(string candidate, string current, bool newer) =>
        Assert.Equal(newer, Marketplace.IsNewer(candidate, current));

    // ----- Installing -----

    [Fact]
    public async Task Install_IsPendingUntilTheNextStart_ThenInPlace()
    {
        var zip = Package("hello", "1.0.0", files: ("Hello.dll", "binary"));
        var installer = new PluginInstaller(Root, new HttpClient());

        Assert.Null(await installer.InstallFileAsync(zip, "hello", Sha(zip)));
        var pending = installer.List().Single();
        Assert.Equal(("", "1.0.0"), (pending.Info.Version, pending.PendingVersion)); // not installed yet

        Assert.Empty(PluginInstaller.ApplyPending(Root));
        var installed = installer.List().Single();
        Assert.Equal(("1.0.0", null), (installed.Info.Version, installed.PendingVersion));
        Assert.True(File.Exists(Path.Combine(Root, "hello", "Hello.dll")));
    }

    [Fact]
    public async Task Update_ReplacesTheOldFiles_AtTheNextStart()
    {
        var installer = new PluginInstaller(Root, new HttpClient());
        var v1 = Package("hello", "1.0.0", files: ("Old.dll", "1"));
        await installer.InstallFileAsync(v1, "hello", Sha(v1));
        PluginInstaller.ApplyPending(Root);

        var v2 = Package("hello", "2.0.0", files: ("New.dll", "2"));
        await installer.InstallFileAsync(v2, "hello", Sha(v2));
        Assert.Equal(("1.0.0", "2.0.0"), (installer.List().Single().Info.Version, installer.List().Single().PendingVersion));
        PluginInstaller.ApplyPending(Root);

        Assert.False(File.Exists(Path.Combine(Root, "hello", "Old.dll")));
        Assert.True(File.Exists(Path.Combine(Root, "hello", "New.dll")));
    }

    [Fact]
    public async Task Remove_AtTheNextStart_AndInstallingAgainCancelsIt()
    {
        var installer = new PluginInstaller(Root, new HttpClient());
        var zip = Package("hello", "1.0.0");
        await installer.InstallFileAsync(zip, "hello", Sha(zip));
        PluginInstaller.ApplyPending(Root);

        installer.Remove("hello");
        Assert.True(installer.List().Single().PendingRemoval);
        await installer.InstallFileAsync(zip, "hello", Sha(zip));
        Assert.False(installer.List().Single().PendingRemoval);

        installer.Remove("hello");
        PluginInstaller.ApplyPending(Root);
        Assert.Empty(installer.List());
    }

    [Fact]
    public async Task Install_WrongSha_IsRefused()
    {
        var zip = Package("hello", "1.0.0");

        var error = await new PluginInstaller(Root, new HttpClient()).InstallFileAsync(zip, "hello", new string('0', 64));

        Assert.Contains("SHA-256", error);
        Assert.False(Directory.Exists(Path.Combine(Root, ".pending", "hello")));
    }

    [Fact]
    public async Task Install_ZipSlip_IsRefused()
    {
        var zip = Package("evil", "1.0.0", files: ("../../outside.txt", "gotcha"));

        var error = await new PluginInstaller(Root, new HttpClient()).InstallFileAsync(zip, "evil", Sha(zip));

        Assert.Contains("outside the plugin's folder", error);
        Assert.False(File.Exists(Path.Combine(Root, "outside.txt")));
        Assert.False(Directory.Exists(Path.Combine(Root, ".pending", "evil")));
    }

    [Theory]
    [InlineData("other", PluginApi.Version, "not \"hello\"")]
    [InlineData("hello", PluginApi.Version + 1, "plugin API")]
    public async Task Install_PackageThatIsntWhatTheIndexSaid_IsRefused(string packageId, int api, string error)
    {
        var zip = Package(packageId, "1.0.0", api);

        Assert.Contains(error, await new PluginInstaller(Root, new HttpClient()).InstallFileAsync(zip, "hello", Sha(zip)));
    }

    [Fact]
    public async Task Install_IncompatibleEntry_NothingDownloaded()
    {
        var handler = new FakeHandler(_ => throw new InvalidOperationException("must not download"));

        var error = await new PluginInstaller(Root, new HttpClient(handler)).InstallAsync(Entry("hello", new string('a', 64), api: 99));

        Assert.Contains("plugin API 99", error);
    }

    [Fact]
    public async Task InstallAsync_DownloadsAndChecks()
    {
        var zip = Package("hello", "1.0.0");
        var handler = new FakeHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(File.ReadAllBytes(zip)),
        });

        Assert.Null(await new PluginInstaller(Root, new HttpClient(handler)).InstallAsync(Entry("hello", Sha(zip))));
        Assert.Equal("hello.zip", handler.LastRequest!.RequestUri!.Segments[^1]);
        Assert.True(File.Exists(Path.Combine(Root, ".pending", "hello", PluginPackageInfo.FileName)));
    }

    [Fact]
    public async Task InstallAsync_FromALocalIndex_FileUrl()
    {
        var zip = Package("local", "0.1.0");
        var entry = Entry("local", Sha(zip)) with { Url = new Uri(zip).AbsoluteUri };
        Assert.StartsWith("file:///", entry.Url);
        Assert.Single(Marketplace.ParseIndex(JsonSerializer.Serialize(new[] { entry }, new JsonSerializerOptions(JsonSerializerDefaults.Web))));

        Assert.Null(await new PluginInstaller(Root, new HttpClient(new FakeHandler(_ => throw new InvalidOperationException()))).InstallAsync(entry));
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }
}
