# CLAUDE.md

## What this is

`Umbraco.Community.ArchivedUsers`: an Umbraco package that archives users (disable, hide from the Users list and pickers, keep an audit). One Razor Class Library with a line-agnostic `Core/` folder, a per-line `Web/` folder and a Lit + Vite client in `Client/` built into the committed `wwwroot/`. Design: `docs/design.md`. Plan: `docs/plan.md`. Spike evidence: `docs/spikes.md`.

## Branches and versioning

- `main` targets Umbraco 18: `Umbraco.Cms.Web.Common` and `Umbraco.Cms.Api.Management` `[18.3.0, 19.0.0)`, npm `@umbraco-cms/backoffice ^18.3.0`. Versions `18.x.y` (`18.0.0-alpha.N` until 1.0).
- `v17` targets Umbraco 17: `[17.8.0, 18.0.0)`, `^17.8.0`. Versions `17.x.y`.
- A version bump touches the csproj `<Version>`, `Client/package.json`, `Client/package-lock.json` and `Client/public/umbraco-package.json` together.
- Fix on `main` first, then cherry-pick to `v17`. `Core/` must stay identical on both branches; per-line differences live in `Web/`, `Client/` and build files only.

## Commands

```bash
# Backend: restore at the floor (lock files), build, test (unit + integration on SQLite)
dotnet restore src/Umbraco.Community.ArchivedUsers.slnx --locked-mode
dotnet build src/Umbraco.Community.ArchivedUsers.slnx -c Release --no-restore
dotnet test src/Umbraco.Community.ArchivedUsers.slnx -c Release --no-build

# Integration tests on SQL Server (any reachable instance)
Tests__Database__DatabaseType=SqlServer \
Tests__Database__SQLServerMasterConnectionString="Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=true;Encrypt=false" \
  dotnet test src/Umbraco.Community.ArchivedUsers.IntegrationTests -c Release

# Client (Lit + TypeScript + Vite); output goes to ../wwwroot, which is committed
cd src/Umbraco.Community.ArchivedUsers/Client
npm ci --ignore-scripts && npm run build && npm test
npm run generate:api          # regenerate src/api/generated from a running demo site (Swashbuckle path on v17)
bash ../../../build/check-xss.sh   # run from the repo root

# Demo site (SQLite; seeds demo users when ArchivedUsersDemo:Seed is true)
dotnet user-secrets set "Umbraco:CMS:Unattended:UnattendedUserPassword" "<pw>" --project demo/Umbraco.Community.ArchivedUsers.DemoSite
dotnet user-secrets set "ArchivedUsersDemo:UserPassword" "<pw>" --project demo/Umbraco.Community.ArchivedUsers.DemoSite
dotnet run --project demo/Umbraco.Community.ArchivedUsers.DemoSite   # https://localhost:44331/umbraco

# End-to-end (demo site running; credentials as environment variables, see tests/e2e/.env.example)
cd tests/e2e && npm ci --ignore-scripts && npx playwright install chromium && npx playwright test

# Release gate
bash build/check-vulnerable.sh
```

## Layout

- `src/Umbraco.Community.ArchivedUsers/Core/`: settings, models, NPoco DTOs and repository, migration plan, `UserVisibility`, `UserStateRules`, `ArchivedUserService` (+ partials), `ArchiveUserLocks`, notifications, handlers. Must not reference `Filter`/`Filter2`, Swashbuckle or `Microsoft.AspNetCore.OpenApi`.
- `src/Umbraco.Community.ArchivedUsers/Web/`: Management API controllers, the `/filter/user` filter (`ArchivedUserListFilterGate` + `ArchivedUserListFilter`), OpenAPI registration (v17 line: Swashbuckle document at `/umbraco/swagger/archived-users/swagger.json`; `main` uses Microsoft.AspNetCore.OpenApi), the composer.
- `src/Umbraco.Community.ArchivedUsers/Client/`: manifests, archive-status global context, conditions, confirm modal, entity and bulk actions, Archived users view, generated API client.
- `src/Umbraco.Community.ArchivedUsers.Tests/`: xUnit + NSubstitute. `src/Umbraco.Community.ArchivedUsers.IntegrationTests/`: NUnit + `Umbraco.Cms.Tests.Integration` (NUnit 3.14, matching Umbraco's test framework).
- `demo/`: demo site (not packed). `tests/e2e/`: Playwright specs.

## Behaviours to preserve

- **The service authorises every write**, per user, because `IUserService` does not (AU-01): core's `IUserPermissionService.AuthorizeAccessAsync` **and** `UserVisibility.CanSee`. The superuser is never a target. Non-admins get `NotPermitted` for unknown keys too (AU-16).
- **A disabled or locked-out performer may change nobody** (`UserStateRules.CanPerform`, P9). "Disabled" means `!IsApproved` (`UserStateRules.IsDisabled`), never `UserState == Disabled`: core reports `LockedOut` before `Disabled`.
- Archive: disable through `IUserService.DisableAsync` one user at a time, rotate the security stamp (AU-02), then record + audit row in one scope. Unarchive deletes the record **before** enabling, inside one scope, so the `UserSaved` handler writes no duplicate row.
- The per-user `ArchiveUserLocks` lock (DI singleton, striped, not re-entrant) is held from the state read to the record write. Archived/Unarchived notifications are published **after** the lock is released.
- The list filter hides only archived **and** disabled users (AU-03), acts only on core's `FilterUserFilterController` / `Filter` (v17 line; `Filter2` on `main`) / GET (AU-10), and fails open to core's response on any exception. Hide switch off = core behaviour exactly.
- GET never writes. Stale records are flagged on read and reconciled at start-up or on the next archive.
- Request models carry only `userIds`, `reason`, `enable`; performer, source and time come from the server. JSON bodies only. Logs carry user keys only, never names, emails or reasons.
- Client: no HTML sinks, no generic confirm modal, literal localisation keys only (`build/check-xss.sh` enforces it). User text is rendered with Lit text bindings.
- Floors never below 17.8.0 / 18.3.0 (AU-02). NuGet ID, migration plan name and state, table and column names, route, OpenAPI document name and every `ArchivedUsers.*` alias are permanent once released.
- `[Category("Gating")]` contract tests are never edited to match core: a failure triggers the fallback named on the test. `Tripwire` failures go to a security review. Only `Recording` tests may be updated, with the observation written in `docs/spikes.md`.
- The repository is public: security reviews, plan reviews and review reports never go in it. `docs/` holds only `design.md`, `plan.md` and `spikes.md`.

## Releasing

1. Bump the four version files together and rebuild the client (`wwwroot/` is committed; CI fails on drift).
2. `bash build/check-vulnerable.sh` must exit 0.
   `bash build/check-release-version.sh v17.x.y` must exit 0 (`publish.yml` runs it, and checks the tag is on `main` or `v17`).
3. Run the **Release checks** workflow by hand on the release branch (Actions → Release checks → Run workflow): Playwright e2e and SQL Server must pass. It does not run on PRs.
4. Merge to `main` (or `v17`), tag `v18.x.y` (or `v17.x.y`) on the merge commit and push the tag. `publish.yml` packs and pushes with NuGet Trusted Publishing.
5. `gh release create <tag> --prerelease --generate-notes` for prereleases.
