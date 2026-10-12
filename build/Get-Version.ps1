<#
  DevHub's version, from git (docs/RELEASING.md):
    N.M.0, where N = the highest major tag "vN" reachable from the commit (only you create those),
    and M = the commits on the first-parent line (main's own history: each push or PR merge) since it.
  The tagged commit itself is N.0.0. Without any vN tag yet: 0.M.0.
  Release tags ("release/1.5.0", made by CI) never look like "vN", so they don't count.

    pwsh build/Get-Version.ps1            # the version of HEAD
    pwsh build/Get-Version.ps1 -Ref main
  Needs the full history (CI checks out with fetch-depth: 0).
#>
param([string]$Ref = "HEAD")
$ErrorActionPreference = "Stop"

$majors = @(git tag --merged $Ref --list "v*" | Where-Object { $_ -match '^v(\d+)$' } | ForEach-Object { [int]$Matches[1] })
if ($LASTEXITCODE -ne 0) { throw "git tag failed (is this a git repository with history?)" }

if ($majors.Count -eq 0) {
    $major = 0
    $minor = [int](git rev-list --first-parent --count $Ref)
} else {
    $major = ($majors | Measure-Object -Maximum).Maximum
    $minor = [int](git rev-list --first-parent --count "v$major..$Ref")
}
if ($LASTEXITCODE -ne 0) { throw "git rev-list failed" }

"$major.$minor.0"
