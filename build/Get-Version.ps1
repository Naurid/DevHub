<#
  DevHub's version, from git (docs/RELEASING.md):
    N.M.0, where N = the highest major tag reachable from the commit (only you create those),
    and M = the commits on the first-parent line (main's own history: each push or PR merge) since it.
  A major tag is "vN", "vN.0" or "vN.0.0" (v2, v2.0, v2.0.0 all mean major 2).
  The tagged commit itself is N.0.0. Without any major tag yet: 0.M.0.
  Release tags ("release/1.5.0", made by CI) never look like that, so they don't count.

    pwsh build/Get-Version.ps1            # the version of HEAD
    pwsh build/Get-Version.ps1 -Ref main
  Needs the full history (CI checks out with fetch-depth: 0).
#>
param([string]$Ref = "HEAD")
$ErrorActionPreference = "Stop"

$majors = @(git tag --merged $Ref --list "v*" |
    Where-Object { $_ -match '^v(\d+)(\.0){0,2}$' } |
    ForEach-Object { [pscustomobject]@{ Major = [int]$Matches[1]; Tag = $_ } })
if ($LASTEXITCODE -ne 0) { throw "git tag failed (is this a git repository with history?)" }

if ($majors.Count -eq 0) {
    $major = 0
    $minor = [int](git rev-list --first-parent --count $Ref)
} else {
    $major = ($majors | Measure-Object -Property Major -Maximum).Maximum
    # The same major written twice (v2.0, then v2 later): count from the most recent of them.
    $minor = ($majors | Where-Object Major -eq $major |
        ForEach-Object { [int](git rev-list --first-parent --count "$($_.Tag)..$Ref") } |
        Measure-Object -Minimum).Minimum
}
if ($LASTEXITCODE -ne 0) { throw "git rev-list failed" }

"$major.$minor.0"
