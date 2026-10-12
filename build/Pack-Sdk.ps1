<#
  Packs the Sdk for plugins: DevHub.Sdk + DevHub.Sdk.Core (.nupkg + .snupkg), with DevHub's version
  (docs/RELEASING.md, docs/PLUGINS.md §9).

  Without -Version it's a local build to try Sdk changes in plugins before releasing them: the version is
  the one git gives this commit plus "-local.<timestamp>" (every pack is new, so NuGet never reuses a
  cached copy), and the packages go to ../local-feed (next to this repository), which the plugins repo
  reads when it exists.

    pwsh build/Pack-Sdk.ps1                       # local feed, e.g. 1.7.0-local.20261012143005
    pwsh build/Pack-Sdk.ps1 -Version 1.7.0 -Out Releases/nuget   # what release.yml does
#>
param(
    [string]$Version,
    [string]$Out = (Join-Path $PSScriptRoot "../../local-feed")
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot

if (-not $Version) {
    $Version = "$(& (Join-Path $PSScriptRoot "Get-Version.ps1"))-local.$(Get-Date -Format yyyyMMddHHmmss)"
}
New-Item -ItemType Directory -Force $Out | Out-Null

foreach ($project in "src/DevHub.Sdk.Core", "src/DevHub.Sdk") {
    dotnet pack (Join-Path $root $project) -c Release -p:Version=$Version -o $Out --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed: $project" }
}

$feed = (Resolve-Path $Out).Path
Write-Output ""
Write-Output "DevHub Sdk $Version in $feed"
Write-Output "Build a plugin with it: dotnet build <plugin> -p:DevHubSdkVersion=$Version"
