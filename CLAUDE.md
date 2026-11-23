# CLAUDE.md

## What this is

`Umbraco.Community.ArchivedUsers`: an Umbraco package that archives users (disable, hide from the Users list and pickers, keep an audit). One Razor Class Library with a line-agnostic `Core/` folder, a per-line `Web/` folder and a Lit + Vite client in `Client/` built into the committed `wwwroot/`. Design: `docs/design.md`. Plan: `docs/plan.md`. Spike evidence: `docs/spikes.md`.

## Lines and versioning

- **One branch, three lines.** `UmbracoMajor` (`17`, `18` or `19`, default `18`, set as a property or an environment variable) picks the CMS range (`Umbraco.Cms.Web.Common` and `Umbraco.Cms.Api.Management` `[17.8.2, 18.0.0-alpha)`, `[18.3.2, 19.0.0-alpha)` or `[19.0.0-rc1, 20.0.0-alpha)`; the `-alpha` keeps the next major's prereleases out), the target framework (`net10.0` for 17 and 18, `net11.0` for 19, so line 19 needs the .NET 11 SDK), the package version, the `UMBRACO_17` / `UMBRACO_18` / `UMBRACO_19` constant, and its own `packages.<line>.lock.json`, `obj/<line>` and `bin/<line>`. 19 defines `UMBRACO_19` only, so every `#if UMBRACO_17 ... #else` takes its 18 arm on 19: a new `#else` that means "18" runs on 19 too. `eng/lines.txt` lists the lines; `build/test.sh` runs the suites once per line. Stable from 18.0.0: no breaking changes within a major.
- **One client bundle serves every line.** `@umbraco-cms/backoffice` is a Vite external, the committed `wwwroot/` is built against `^17.8.0`, and CI type-checks the source against `^18.3.0` and `19.0.0-rc1` (exact: the RC has no npm tag) (`docs/spikes.md`, "One client bundle"). Split into two bundles only if a line ever needs a different request payload.
- The package version is `<UmbracoMajor>.<PackageMinor>.<PackagePatch>[-suffix]` from `Directory.Build.props` for 17 and 18; a 19 tag carries its whole version (`release/19.0.0-rc.1`). Client version files stay `0.0.0`; the pack step stamps the line's version into the nupkg's manifest.
- `Core/` is shared by every line; per-line differences live in `Web/`, `Client/`, tests behind `#if UMBRACO_17` / `#if UMBRACO_19`, and build files only. `v17` is frozen history: nothing merges to it.

## Commands

```bash
# Backend: restore at the floor (lock files), build, test (unit + integration on SQLite). Default line is 18.
bash build/test.sh                 # every line in eng/lines.txt; or: bash build/test.sh 17 18
# Line 19 targets net11.0: put the .NET 11 SDK first on PATH for it (bash build/test.sh 19, LINES=19 PACK_VERSION=19.0.0-rc.1 bash build/pack.sh)
dotnet restore src/Umbraco.Community.ArchivedUsers.slnx -p:UmbracoMajor=17 --locked-mode   # one line, locked
dotnet build src/Umbraco.Community.ArchivedUsers.slnx -c Release --no-restore -p:UmbracoMajor=17
dotnet test src/Umbraco.Community.ArchivedUsers.slnx -c Release --no-build -p:UmbracoMajor=17

# Integration tests on SQL Server (any reachable instance)
Tests__Database__DatabaseType=SqlServer \
Tests__Database__SQLServerMasterConnectionString="Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=true;Encrypt=false" \
  dotnet test src/Umbraco.Community.ArchivedUsers.IntegrationTests -c Release -p:UmbracoMajor=18

# Client (Lit + TypeScript + Vite); output goes to ../wwwroot, which is committed
cd src/Umbraco.Community.ArchivedUsers/Client
npm ci --ignore-scripts && npm run build && npm test
npm run generate:api          # regenerate src/api/generated from a running demo site; the OpenAPI document path depends on the line (swagger on 17, openapi on 18), so run the demo with -p:UmbracoMajor=18; the committed client is the one bundle
bash ../../../build/check-xss.sh   # run from the repo root

# Demo site (SQLite; seeds demo users when ArchivedUsersDemo:Seed is true)
dotnet user-secrets set "Umbraco:CMS:Unattended:UnattendedUserPassword" "<pw>" --project demo/Umbraco.Community.ArchivedUsers.DemoSite
dotnet user-secrets set "ArchivedUsersDemo:UserPassword" "<pw>" --project demo/Umbraco.Community.ArchivedUsers.DemoSite
dotnet run --project demo/Umbraco.Community.ArchivedUsers.DemoSite -p:UmbracoMajor=18   # https://localhost:44331/umbraco; use a separate database per line

# End-to-end (demo site running; credentials as environment variables, see tests/e2e/.env.example)
cd tests/e2e && npm ci --ignore-scripts && npx playwright install chromium && npx playwright test

# Release gate
bash build/check-vulnerable.sh
```

## Layout

- `src/Umbraco.Community.ArchivedUsers/Core/`: settings, models, NPoco DTOs and repository, migration plan, `UserVisibility`, `UserStateRules`, `ArchivedUserService` (+ partials), `ArchiveUserLocks`, notifications, handlers. Must not reference `Filter`/`Filter2`, Swashbuckle or `Microsoft.AspNetCore.OpenApi`.
- `src/Umbraco.Community.ArchivedUsers/Web/`: Management API controllers, the `/filter/user` filter (`ArchivedUserListFilterGate` + `ArchivedUserListFilter`), OpenAPI registration, the composer.
- `src/Umbraco.Community.ArchivedUsers/Client/`: manifests, archive-status global context, conditions, confirm modal, entity and bulk actions, Archived users view, generated API client.
- `src/Umbraco.Community.ArchivedUsers.Tests/`: xUnit + NSubstitute. `src/Umbraco.Community.ArchivedUsers.IntegrationTests/`: NUnit + `Umbraco.Cms.Tests.Integration` (NUnit 3.14, matching Umbraco's test framework).
- `demo/`: demo site (not packed). `tests/e2e/`: Playwright specs.

## Behaviours to preserve

- **The service authorises every write**, per user, because `IUserService` does not (AU-01): core's `IUserPermissionService.AuthorizeAccessAsync` **and** `UserVisibility.CanSee`. The superuser is never a target. Non-admins get `NotPermitted` for unknown keys too (AU-16).
- **A disabled or locked-out performer may change nobody** (`UserStateRules.CanPerform`, P9). "Disabled" means `!IsApproved` (`UserStateRules.IsDisabled`), never `UserState == Disabled`: core reports `LockedOut` before `Disabled`.
- Archive: disable through `IUserService.DisableAsync` one user at a time, rotate the security stamp (AU-02), then record + audit row in one scope. Unarchive deletes the record **before** enabling, inside one scope, so the `UserSaved` handler writes no duplicate row.
- The per-user `ArchiveUserLocks` lock (DI singleton, striped, not re-entrant) is held from the state read to the record write. Archived/Unarchived notifications are published **after** the lock is released.
- The list filter hides only archived **and** disabled users (AU-03), acts only on core's `FilterUserFilterController` / the line's bound action (`Filter` on 17, `Filter2` on 18 and 19) / GET (AU-10), and fails open to core's response on any exception. Hide switch off = core behaviour exactly.
- GET never writes. Stale records are flagged on read and reconciled at start-up or on the next archive.
- Request models carry only `userIds`, `reason`, `enable`; performer, source and time come from the server. JSON bodies only. Logs carry user keys only, never names, emails or reasons.
- Client: no HTML sinks, no generic confirm modal, literal localisation keys only (`build/check-xss.sh` enforces it). User text is rendered with Lit text bindings.
- Floors never below 17.8.2 / 18.3.2 / 19.0.0-rc1 (19.0.0 once it ships) (AU-02; 17.8.2 / 18.3.2 for GHSA-348f-9mg8-rhv4). Check `gh api /repos/umbraco/Umbraco-CMS/security-advisories` before each release: osv and `check-vulnerable.sh` don't see Umbraco's repository advisories. NuGet ID, migration plan name and state, table and column names, route, OpenAPI document name and every `ArchivedUsers.*` alias are permanent once released.
- `[Category("Gating")]` contract tests are never edited to match core: a failure triggers the fallback named on the test. `Tripwire` failures go to a security review. Only `Recording` tests may be updated, with the observation written in `docs/spikes.md`.
- The repository is public: security reviews, plan reviews and review reports never go in it. `docs/` holds only `design.md`, `plan.md` and `spikes.md`.

## Releasing

1. Bump `PackageMinor`, `PackagePatch` and `PackageVersionSuffix` in `Directory.Build.props`; lines 17 and 18 move together (a 19 release takes its version from its tag). The client version files stay `0.0.0`.
2. `bash build/check-vulnerable.sh` and `UmbracoMajor=17 bash build/check-vulnerable.sh` must exit 0.
   `UmbracoMajor=19 bash build/check-vulnerable.sh` with the .NET 11 SDK first on PATH must exit 0 too.
   `bash build/check-release-version.sh release/<minor>.<patch>[-suffix]` must exit 0 (`publish.yml` runs it, and checks the tag is on `main`).
3. Run the **Release checks** workflow by hand on the release branch (Actions → Release checks → Run workflow): Playwright e2e and SQL Server must pass on every line, except that 19 e2e is advisory while 19 is a release candidate (a failure is written to the run summary for the release notes). It does not run on PRs.
4. Squash-merge to `main`, tag `release/<minor>.<patch>[-suffix]` on the merge commit and push the tag. `publish.yml` packs one nupkg per line (17 and 18) and pushes with NuGet Trusted Publishing. A three-part tag `release/19.<minor>.<patch>[-suffix]` (for example `release/19.0.0-rc.1`) packs 19 only, at the tag's version, and leaves `PackageMinor` and `PackagePatch` alone.
5. `gh release create release/<minor>.<patch>[-suffix] --generate-notes` for each line's version (add `--prerelease` only for a `-beta`/`-rc` tag), newest stable line Latest: a 19 release candidate is never Latest.
