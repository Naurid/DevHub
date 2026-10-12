# DevHub plugins

Plugins for [DevHub](https://github.com/Naurid/DevHub), offered in its marketplace (DevHub: Settings →
Plugins → Marketplace). Each folder in `plugins/` is one plugin; every push to `main` publishes the
plugins that changed.

## Layout

```
plugins/<Name>/          one plugin: <Name>.csproj, plugin.json, its plugin class, ViewModels/, Views/
build/Get-PluginVersion.ps1   a plugin's version, from git
build/Pack-Plugin.ps1    builds one plugin and packs <id>-<version>.zip (plugin.json + its DLLs)
build/Publish-Plugins.ps1     CI: releases the plugins whose version changed and updates the index
.github/workflows/       ci.yml (build, vulnerable packages), publish.yml (releases + index)
Directory.Build.props    shared settings: the Sdk packages (NuGet), Avalonia / CommunityToolkit / DI build-only
```

## Writing a plugin

1. Copy `plugins/HelloWorld` to `plugins/<YourPlugin>` and rename:
   - the `.csproj`;
   - `plugin.json`: a new `id` (lowercase, `yourname.plugin-name`), name, description, category.
     Leave out `version`, which is computed;
   - the plugin class: its `PluginInfo` id must equal plugin.json's.
2. Build: `dotnet build plugins/<YourPlugin>`. The Sdk comes from nuget.org (`DevHub.Sdk`,
   `DevHub.Sdk.Core`): the newest release by default. DevHub's plugin guide (docs/PLUGINS.md in DevHub)
   explains the extension points (tool windows, documents, actions, settings pages…).
3. Try it in DevHub before publishing:
   - `pwsh build/Pack-Plugin.ps1 -Folder plugins/<YourPlugin> -Version 0.0.1`;
   - write an `index.json` with one entry whose `url` is the zip's `file:///…` path and `sha256` its
     hash (the script prints both);
   - add that index.json's path to DevHub's Settings → Plugins → Marketplace → Sources, Refresh,
     Install, and restart.

Keep the versions of Avalonia, CommunityToolkit.Mvvm and Microsoft.Extensions.DependencyInjection
in `Directory.Build.props` equal to DevHub's (DevHub provides them at run time). `apiVersion` in
plugin.json must match DevHub's plugin API version (`PluginApi.Version`); DevHub refuses plugins
made for another one, and the build checks it against the Sdk package.

## A plugin that needs another one

List the other plugin's id in `requires` (plugin.json and the plugin class's `PluginInfo`). To use its
types, reference its project (`<ProjectReference Include="..\OtherPlugin\OtherPlugin.csproj" />`).
`Pack-Plugin.ps1` leaves the other plugin's DLLs out of your package (it prints which): DevHub installs
the other plugin with yours and loads its DLLs first, so there's one copy, its own.

## Unreleased Sdk changes (local feed)

To build against Sdk changes that aren't released yet, clone this repository next to DevHub (both in
the same folder) and, in DevHub, run `pwsh build/Pack-Sdk.ps1`. It packs the Sdk with a version like
`1.7.0-local.20261012143005` into `local-feed/` beside the two repositories, which this repository's
build reads whenever that folder exists. Then:

```
dotnet build plugins/<YourPlugin> -p:DevHubSdkVersion=1.7.0-local.20261012143005
```

(`-p:DevHubSdkVersion=*-*` takes the newest one, local packs included; `-p:DevHubLocalFeed=<folder>`
reads another folder.) Without the property the build goes back to the newest release.

## Versions (each plugin has its own)

A plugin's version is **N.M.0**:

- **N (major)** is the highest tag `<plugin-id>/vN`, e.g. `git tag naurid.hello-world/v2 && git push origin naurid.hello-world/v2`.
- **M (minor)** is the number of pushes or merges on `main` since that tag *that touched the
  plugin's folder*. Changing one plugin never changes another's version.
- Before the first major tag the version is `0.M.0`.

`pwsh build/Get-PluginVersion.ps1 -Folder plugins/HelloWorld` shows the version it would get.

## Publishing (automatic)

On every push to `main` (and on a plugin's major tag), `publish.yml`:

1. runs CI;
2. for each plugin, computes its version; if the release `<id>-<version>` doesn't exist yet, it
   builds and packs the plugin and creates that release with the zip;
3. replaces the plugin's entry in **`index.json`**, a file on the release named `index` (version,
   download URL, SHA-256, size).

DevHub reads `https://github.com/Naurid/DevHub-Plugins/releases/download/index/index.json`.

## One-time GitHub settings (like DevHub's, see DevHub's docs/RELEASING.md)

- Public repository.
- Ruleset on `main` (pull requests and the CI check required; you on the bypass list).
- Ruleset on tags `*/v*` (only you).
- Actions: read-only by default, approval for first-time contributors.
- Dependabot alerts and secret scanning on.
