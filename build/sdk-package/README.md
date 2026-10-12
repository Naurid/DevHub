# DevHub Sdk

What [DevHub](https://github.com/Naurid/DevHub) plugins build against:

- **DevHub.Sdk.Core**: no UI. Plugin model (`PluginInfo`, requirements), the settings store
  (`ISettingsSection`: one section per plugin), marketplace types, `FuzzyMatcher`, formatting helpers.
- **DevHub.Sdk**: the Avalonia building blocks. `IPlugin`, base classes for ViewModels, documents, tool
  windows, dialogs and settings pages; the action system; service interfaces (dialogs, status, theme);
  integration hooks between plugins; the design system (tokens, controls, icons).

The package version is the DevHub version it comes from. A plugin runs in any DevHub with the same
**plugin API version** (`PluginApi.Version`, also the `devhub-plugin-api-N` tag). DevHub already
provides the Sdk, Avalonia, CommunityToolkit.Mvvm and Microsoft.Extensions.DependencyInjection at run
time, so a plugin references them build-only and never ships them:

```xml
<PackageReference Include="DevHub.Sdk" Version="1.*" ExcludeAssets="runtime" />
<PackageReference Include="DevHub.Sdk.Core" Version="1.*" ExcludeAssets="runtime" />
```

For a project with a `plugin.json` next to it, the build checks that its `apiVersion` matches this Sdk.

Writing a plugin: the [DevHub-Plugins](https://github.com/Naurid/DevHub-Plugins) repository (a sample
plugin, packing and publishing to DevHub's marketplace) and `docs/PLUGINS.md` in DevHub.
