<#
  Puts the plugins DevHub bundles (build/bundled-plugins.json, plus what they require) into <Out>\<id>\, from the
  marketplace index of Naurid/DevHub-Plugins: the newest version of each, made for this DevHub's plugin API
  (PluginApi.Version). Each download is checked against the index's SHA-256, and no entry of a package may land
  outside its folder. DevHub loads them from plugins\ next to its executable (docs/PLUGINS.md §9, "Bundled").

    pwsh build/Get-BundledPlugins.ps1 -Out artifacts/publish/win-x64/plugins      # release.yml
    pwsh build/Get-BundledPlugins.ps1 -Out src/DevHub.App/bin/Debug/net10.0/plugins   # a Debug build does this once
    pwsh build/Get-BundledPlugins.ps1 -Out <folder> -Index C:\plugins\index.json  # another index (a local file works)

  Fails (nothing half-written) when a plugin is missing from the index or made for another plugin API.
#>
param(
    [Parameter(Mandatory)] [string]$Out,
    [string]$Index = "https://github.com/Naurid/DevHub-Plugins/releases/download/index/index.json",
    [string]$List = (Join-Path $PSScriptRoot "bundled-plugins.json")
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot

# The plugin API this DevHub offers (written once, in Marketplace.cs).
$source = Get-Content (Join-Path $root "src/DevHub.Sdk.Core/Plugins/Marketplace/Marketplace.cs") -Raw
$api = [int][regex]::Match($source, '(?<=const int Version = )\d+').Value
if ($api -le 0) { throw "PluginApi.Version not found in Marketplace.cs." }

$entries = @(if ($Index -match '^https://') { Invoke-RestMethod $Index } else { Get-Content $Index -Raw | ConvertFrom-Json })

# The listed plugins and, recursively, what they require.
$wanted = [Collections.Generic.List[object]]::new()
$queue = [Collections.Generic.Queue[string]]::new()
@(Get-Content $List -Raw | ConvertFrom-Json) | ForEach-Object { $queue.Enqueue($_) }
while ($queue.Count -gt 0) {
    $id = $queue.Dequeue()
    if ($wanted | Where-Object { $_.id -eq $id }) { continue }
    $entry = $entries | Where-Object { $_.id -eq $id } | Select-Object -First 1
    if (-not $entry) { throw "The index ($Index) has no plugin '$id'." }
    if ([int]$entry.apiVersion -ne $api) { throw "$($entry.name) $($entry.version) is made for plugin API $($entry.apiVersion); this DevHub has $api." }
    $wanted.Add($entry)
    @($entry.requires) | Where-Object { $_ } | ForEach-Object { $queue.Enqueue($_) }
}

# Download and check everything first, then unpack: a failure leaves <Out> as it was.
$temp = Join-Path ([IO.Path]::GetTempPath()) "devhub-bundled-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force $temp | Out-Null
try {
    foreach ($entry in $wanted) {
        $zip = Join-Path $temp "$($entry.id).zip"
        if ($entry.url -match '^file:///') { Copy-Item ([Uri]$entry.url).LocalPath $zip } else { Invoke-WebRequest $entry.url -OutFile $zip }
        $sha = (Get-FileHash $zip -Algorithm SHA256).Hash
        if ($sha -ne $entry.sha256.ToUpperInvariant()) { throw "$($entry.name) $($entry.version): the download doesn't match the index's SHA-256." }

        $target = Join-Path $temp "unpacked\$($entry.id)"
        $targetRoot = [IO.Path]::GetFullPath($target) + [IO.Path]::DirectorySeparatorChar
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [IO.Compression.ZipFile]::OpenRead($zip)
        try {
            foreach ($item in $archive.Entries) {
                $path = [IO.Path]::GetFullPath((Join-Path $target $item.FullName))
                if (-not $path.StartsWith($targetRoot, [StringComparison]::OrdinalIgnoreCase)) { throw "$($entry.name): '$($item.FullName)' would land outside its folder." }
                if ($item.FullName.EndsWith('/')) { New-Item -ItemType Directory -Force $path | Out-Null; continue }
                New-Item -ItemType Directory -Force (Split-Path $path) | Out-Null
                [IO.Compression.ZipFileExtensions]::ExtractToFile($item, $path, $true)
            }
        } finally {
            $archive.Dispose()
        }
        if (-not (Test-Path (Join-Path $target "plugin.json"))) { throw "$($entry.name): the package has no plugin.json." }
    }

    New-Item -ItemType Directory -Force $Out | Out-Null
    foreach ($entry in $wanted) {
        $folder = Join-Path $Out $entry.id
        if (Test-Path $folder) { Remove-Item -Recurse -Force $folder }
        Move-Item (Join-Path $temp "unpacked\$($entry.id)") $folder
        Write-Output "$($entry.name) $($entry.version) → $folder"
    }
} finally {
    Remove-Item -Recurse -Force $temp -ErrorAction SilentlyContinue
}
