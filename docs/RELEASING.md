# Releasing DevHub

How DevHub gets from a push to the people who use it: versions, the automated checks, the
release build, and the GitHub settings that keep it safe.

## Versions

DevHub's version is **N.M.0**, worked out from git by `build/Get-Version.ps1`:

- **N (major)** is the highest *major tag* reachable from the commit: `v1`, `v2`… (`v2.0` and `v2.0.0` work too). Only you
  create these (a protected tag rule).
- **M (minor)** is the number of commits on `main`'s own line since that tag: each push to `main`
  and each merged pull request counts as one. The tagged commit itself is `N.0.0`.
- Before the first major tag the version is `0.M.0`.
- CI tags each release `release/N.M.0`. Those tags never look like `vN`, so they don't count.

```
pwsh build/Get-Version.ps1        # the version HEAD would get
git tag v2; git push origin v2    # next release is 2.0.0, then 2.1.0, 2.2.0…
```

The first time, tag the commit you want as 1.0.0: `git tag v1 && git push origin v1`.

## What runs on GitHub (`.github/`)

| Workflow | When | What |
|---|---|---|
| `ci.yml` | pull requests; inside every release (a push to `main`); on demand (Actions → CI → Run workflow, for a branch without a pull request) | restore (NuGet packages cached between runs), build with warnings as errors, tests (results as an artifact), vulnerable NuGet packages (fails on any), SBOM (CycloneDX, artifact); on pull requests also GitHub's dependency review |
| `codeql.yml` | pull requests, Mondays (the baseline on `main`), on demand | CodeQL security analysis of the C# code (`security-extended` queries, no build, on Linux: a few minutes); findings appear under Security → Code scanning |

Dependency review and CodeQL need a public repository (or the paid GitHub Advanced Security): while
the repo is private they are skipped (shown as "skipped", which counts as passing).

Typical times: CI 5–8 minutes (Windows runners, and the tests run real git / dotnet / cargo / go),
CodeQL 3–6, a release about 10 in total. A branch push alone runs nothing: open a pull request
(a draft is fine) or use Run workflow.
| `dependabot.yml` | weekly | pull requests that update NuGet packages and the workflows' actions |

Every workflow can only read the repository unless a job asks for more.

## One-time GitHub settings (yours)

1. **Settings → General → Danger Zone → Change visibility → Public.** Public means anyone can
   *read* and download. Only you (and collaborators you add) can push.
2. **Settings → Rules → Rulesets → New branch ruleset** "main":
   - Target: the default branch.
   - Restrict deletions; block force pushes.
   - Require a pull request before merging.
   - Require status checks to pass: `build-and-test` (CI) and `analyze` (CodeQL). They appear in
     the list after they've run once.
   - Bypass list: add yourself (Repository admin), so you can still push straight to `main`.
3. **Settings → Rules → Rulesets → New tag ruleset** "major versions":
   - Target tags: `v*`.
   - Restrict creations, updates and deletions.
   - Bypass list: yourself only. Now only you can bump the major version.
4. **Settings → Actions → General:**
   - Workflow permissions: **Read repository contents** (the default; jobs that publish ask for
     more themselves).
   - Fork pull request workflows: **Require approval for first-time contributors**.
   - Untick "Allow GitHub Actions to create and approve pull requests".
5. **Settings → Code security:**
   - turn on the Dependency graph (dependency review in CI fails without it), Dependabot alerts,
     Dependabot security updates, and secret scanning with push
     protection;
   - leave CodeQL "default setup" off, since `codeql.yml` does it.

What strangers can do on a public repository: read it, fork it into their own account, and open
pull requests. Nothing reaches `main` unless you merge it, and their pull requests run the checks
without access to any secrets.

## Releases (`.github/workflows/release.yml`)

Runs on every push to `main` and on every major tag `vN`. It first runs the whole CI (build,
tests, security checks); nothing is published if that fails. Then:

1. **Version** from git. If `release/<version>` already exists (e.g. you tagged the commit main
   was just released from), it stops: nothing to do.
2. **Publish** a self-contained win-x64 build (`src/DevHub.App/Properties/PublishProfiles/win-x64.pubxml`)
   stamped with the version.
3. **Pack** with Velopack (`vpk`, same version as the `Velopack` package in DevHub.App):
   - `DevHub-win.msi` for new users who want an .msi;
   - `DevHub-win-Setup.exe`, the same installer as an .exe;
   - `DevHub-win-Portable.zip`, which runs without installing (no updates);
   - `DevHub-<version>-full.nupkg`, plus a `-delta` package once a previous release exists: what
     installed copies download to update;
   - `RELEASES` / `releases.win.json`: the list the app reads to see what's new.
4. **Sdk packages** `DevHub.Sdk` and `DevHub.Sdk.Core` (with `.snupkg` symbols), with the same
   version, pushed to **nuget.org** with Trusted Publishing once it is set up (see below). Plugins
   build against them.
5. **GitHub Release** "DevHub <version>", tagged `release/<version>`, with the commit subjects
   since the last release as notes. The installers are also attached to the workflow run as an
   artifact.

DevHub installs per user (no admin rights needed), in `%LocalAppData%\DevHub`, so it can update
itself; settings stay in `%AppData%\DevHub`.

Try it locally (the `vpk` tool as a local install):

```
dotnet tool install vpk --version 1.2.161 --tool-path .tools
dotnet publish src/DevHub.App -p:PublishProfile=win-x64 -p:Version=0.0.1
.tools/vpk pack --packId DevHub --packVersion 0.0.1 --packDir artifacts/publish/win-x64 --mainExe DevHub.App.exe --msi
```

### The Sdk on nuget.org

Plugins (in Naurid/DevHub-Plugins and anyone else's) reference the Sdk as NuGet packages. Their
version is DevHub's, and `PluginApi.Version` is written into them (the `devhub-plugin-api-N` tag,
and a build check against the plugin's `plugin.json`). Packaging settings are in
`build/SdkPackage.props`; `build/Pack-Sdk.ps1` packs both.

The release pushes them with **Trusted Publishing**: no API key is stored anywhere. The job asks
GitHub for a signed token saying "this is Naurid/DevHub, workflow release.yml", nuget.org checks it
against your policy and hands back an API key that lasts one hour (`NuGet/login` in `release.yml`).

One-time setup (yours):

1. Sign in to **nuget.org** (a Microsoft account) and choose the profile name the packages are
   published under.
2. Your name → **Trusted Publishing → Add**: owner yourself; Repository Owner `Naurid`, Repository
   `DevHub`, Workflow File `release.yml` (the file name only), Environment empty.
3. In GitHub, **Settings → Secrets and variables → Actions → Variables → New repository variable**:
   `NUGET_USER` = your nuget.org profile name (not your email). It's not a secret: the policy is
   what protects publishing.
4. Publish within **7 days** (any push to `main`): while the repository is private, the policy starts
   out temporarily active, and the first successful publish locks it to this repository for good.
   If the 7 days pass, restart the window on the policy's page.

From then on, every release pushes both packages. Until `NUGET_USER` exists, the release still packs
them but skips the push. A version on nuget.org can't be deleted or replaced, only unlisted.

**Try Sdk changes before releasing them:** `pwsh build/Pack-Sdk.ps1` packs the Sdk as
`<version>-local.<timestamp>` into `../local-feed` (next to this repository). The plugins repo reads
that folder when it exists: build a plugin with `-p:DevHubSdkVersion=<the printed version>`
(DevHub-Plugins' README).

### Signing (later)

Unsigned installers make Windows SmartScreen say "Windows protected your PC" the first time
(More info → Run anyway). To sign, add two repository secrets (Settings → Secrets and variables →
Actions): `SIGNING_PFX_BASE64` (the .pfx certificate, base64) and `SIGNING_PASSWORD`. The release
workflow signs as soon as they exist; no other change is needed. Azure Trusted Signing works too
(`vpk pack --azureTrustedSignFile`), with a small change to the pack step.

## Updates inside DevHub

- An installed DevHub checks GitHub Releases quietly about 5 seconds after it starts (Settings →
  Updates turns this off). Nothing changes until the user clicks.
- If there's a newer version, the status bar says **"DevHub 1.6.0 is available — Update and
  restart"**. Clicking it downloads the update (a small delta when possible, with progress in the
  status bar), closes DevHub the normal way (open tabs saved, unsaved template edits asked
  about), swaps the version and starts the new one.
- **Settings → Updates** shows the version, Check Now, Update and Restart, Release Notes, and the
  startup-check toggle. **Help → Check for Updates** does the check from the menu or Find Action.
- A development build (`dotnet run`, version `0.0.0-dev`) and the portable zip never update; they
  say so.

### Rehearse an update locally (before trusting a real release)

1. Build and pack version 0.0.1 into a folder, and install it: run its `Setup.exe` (this installs
   DevHub for your Windows user; uninstall later in Settings → Apps).
2. Pack 0.0.2 into the **same** output folder.
3. Start the installed DevHub with `DEVHUB_UPDATE_SOURCE` set to that folder (instead of GitHub):

   ```
   $env:DEVHUB_UPDATE_SOURCE = "C:\path\to\Releases"; & "$env:LOCALAPPDATA\DevHub\current\DevHub.App.exe"
   ```

4. The status bar offers 0.0.2; Update and restart; DevHub comes back as 0.0.2 (Settings → Updates).
