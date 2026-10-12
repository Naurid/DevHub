# Releasing DevHub

How DevHub gets from a push to the people who use it: versions, the automated checks, the
release build, and the GitHub settings that keep it safe.

## Versions

DevHub's version is **N.M.0**, worked out from git by `build/Get-Version.ps1`:

- **N (major)** is the highest *major tag* `vN` reachable from the commit: `v1`, `v2`… Only you
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
| `ci.yml` | every push and pull request | restore, build with warnings as errors, tests (results as an artifact), vulnerable NuGet packages (fails on any), SBOM (CycloneDX, artifact); on pull requests also GitHub's dependency review |
| `codeql.yml` | pull requests, pushes to `main`, Mondays | CodeQL security analysis of the C# code; findings appear under Security → Code scanning |
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
   - turn on Dependabot alerts, Dependabot security updates, and secret scanning with push
     protection;
   - leave CodeQL "default setup" off, since `codeql.yml` does it.

What strangers can do on a public repository: read it, fork it into their own account, and open
pull requests. Nothing reaches `main` unless you merge it, and their pull requests run the checks
without access to any secrets.
