<#
  DevHub's version, from git (docs/RELEASING.md):
    N.M.P, where N = the highest major tag reachable from the commit (only you create those), and M.P come from the
    commits on the first-parent line (main's own history: each push or PR merge) since it:
      - a fix (the merge of a Fix/, Hotfix/ or Bugfix/ branch or pull request, or a commit titled "fix: …") adds 1 to P;
      - anything else adds 1 to M and starts P again at 0.
  A major tag is "vN", "vN.0" or "vN.0.0" (v2, v2.0, v2.0.0 all mean major 2).
  The tagged commit itself is N.0.0. Without any major tag yet: 0.M.P.
  Release tags ("release/1.5.0", made by CI) never look like that, so they don't count.

    pwsh build/Get-Version.ps1            # the version of HEAD
    pwsh build/Get-Version.ps1 -Ref main
  Needs the full history (CI checks out with fetch-depth: 0).
#>
param([string]$Ref = "HEAD")
$ErrorActionPreference = "Stop"

# Fixes count as patches from here on; up to this commit (main when the rule came in, 1.26.0) every commit was a minor,
# and counting the older fixes as patches now would make the version go back (installed copies wouldn't update).
$PatchesSince = "57af166d656c0440afc063e4c84da6756810a7e3"
$FixPattern = "^(Merge (branch '|pull request #\d+ from [^/\s]+/)(fix|hotfix|bugfix)/|(fix|hotfix|bugfix)(\(.*\))?!?:)"

$majors = @(git tag --merged $Ref --list "v*" |
    Where-Object { $_ -match '^v(\d+)(\.0){0,2}$' } |
    ForEach-Object { [pscustomobject]@{ Major = [int]$Matches[1]; Tag = $_ } })
if ($LASTEXITCODE -ne 0) { throw "git tag failed (is this a git repository with history?)" }

$range = $Ref
$major = 0
if ($majors.Count -gt 0) {
    $major = ($majors | Measure-Object -Property Major -Maximum).Maximum
    # The same major written twice (v2.0, then v2 later): count from the most recent of them.
    $range = ($majors | Where-Object Major -eq $major |
        Sort-Object { [int](git rev-list --first-parent --count "$($_.Tag)..$Ref") } |
        Select-Object -First 1 | ForEach-Object { "$($_.Tag)..$Ref" })
}

$before = @{}
git rev-list --first-parent $PatchesSince 2>$null | ForEach-Object { $before[$_] = $true }
$minor = 0
$patch = 0
foreach ($line in @(git log --first-parent --reverse --format="%H%x09%s" $range)) {
    $sha, $subject = $line -split "`t", 2
    if (-not $before.ContainsKey($sha) -and $subject -match $FixPattern) {
        $patch++
    } else {
        $minor++
        $patch = 0
    }
}
if ($LASTEXITCODE -ne 0) { throw "git log failed" }

"$major.$minor.$patch"
