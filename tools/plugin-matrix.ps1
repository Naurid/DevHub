<#
  Starts DevHub once per plugin selection and reports whether it stays up and which plugins it
  loaded. Use it after changing the shell: a shell that still names a plugin, or that breaks a plugin,
  shows up here. (Plugins have their own matrix: build/Test-Plugins.ps1 in Naurid/DevHub-Plugins.)

  Plugins: the test plugin (tests/DevHub.TestPlugin, loaded from its build folder through DEVHUB_PLUGIN_PATHS,
  so always on) and the bundled ones a Debug build fetched into bin\Debug\net10.0\plugins (build/Get-BundledPlugins.ps1).

  Needs the Debug build (it validates every DI registration at startup and prints
  "DevHub plugins: found …; loaded …" to stdout). Each run gets its own scratch DEVHUB_DATA_DIR
  under %TEMP%, with a repository tab (this repo) and a Templates tab open, so their views load.

  Cases: everything (first start: the picker opens), nothing but the test plugin, each plugin alone, plus -Extra.
    pwsh tools/plugin-matrix.ps1
    pwsh tools/plugin-matrix.ps1 -Extra "devhub.projects,devhub.git"
#>
param(
    [string]$Exe = (Join-Path $PSScriptRoot "..\src\DevHub.App\bin\Debug\net10.0\DevHub.App.exe"),
    [string[]]$Extra = @(),
    [int]$Seconds = 7
)

$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$root = Join-Path $env:TEMP "devhub-plugin-matrix"
if (-not (Test-Path $Exe)) { throw "Build first (dotnet build): $Exe not found." }
$testPlugin = Join-Path $repo "tests\DevHub.TestPlugin\bin\Debug\net10.0"
if (-not (Test-Path (Join-Path $testPlugin "plugin.json"))) { throw "Build first (dotnet build): the test plugin isn't in $testPlugin." }
$env:DEVHUB_PLUGIN_PATHS = $testPlugin

function Start-Case([string]$name, $enabled) {
    $dir = Join-Path $root ($name -replace '[^A-Za-z0-9+.]', '_')
    Remove-Item -Recurse -Force $dir -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force (Join-Path $dir "DevHub") | Out-Null
    $settings = @{ editor = @{ openDocuments = @("Repository:$repo", "Generate"); activeDocument = "Generate" } }
    if ($null -ne $enabled) { $settings.enabledPlugins = @($enabled) }
    $settings | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $dir "DevHub\settings.json")

    $env:DEVHUB_DATA_DIR = $dir
    $out = Join-Path $dir "stdout.txt"
    $err = Join-Path $dir "stderr.txt"
    $p = Start-Process -FilePath $Exe -PassThru -RedirectStandardError $err -RedirectStandardOutput $out
    Start-Sleep $Seconds
    $report = (Get-Content $out -ErrorAction SilentlyContinue | Select-String "DevHub plugins: ") -replace "DevHub plugins: ", ""
    if ($p.HasExited) {
        Write-Host ("{0,-32} EXITED ({1})" -f $name, $p.ExitCode)
        Get-Content $err -TotalCount 12 | ForEach-Object { Write-Host "    $_" }
    } elseif ((Get-Process -Id $p.Id).MainWindowHandle -eq 0) {
        # Alive but no window: startup is stuck (e.g. a DI cycle through the DocumentManager).
        Write-Host ("{0,-32} NO WINDOW (startup hangs)  {1}" -f $name, ($report -replace "^found [^;]*; ", ""))
        Stop-Process $p -Confirm:$false
    } else {
        Write-Host ("{0,-32} ok   {1}" -f $name, ($report -replace "^found [^;]*; ", ""))
        Stop-Process $p -Confirm:$false
    }
    return $report
}

$report = Start-Case "everything (first start)" $null
# "found a, b, c; loaded …" → the installed plugin ids.
$ids = if ("$report" -match "found ([^;]*);") { $Matches[1] -split ",\s*" | Where-Object { $_ } } else { @() }
Write-Host "installed: $($ids -join ', ')"

Start-Case "none (test plugin only)" @() | Out-Null
foreach ($id in $ids) { Start-Case "$id alone" @($id) | Out-Null }
foreach ($combo in $Extra) { Start-Case $combo ($combo -split ",\s*") | Out-Null }
