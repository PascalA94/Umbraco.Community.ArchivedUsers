# Archived Users v0.1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Execution (Pascal, 2026-10-30): hybrid.** Native build with `superpowers:executing-plans`, plus three milestone reviews by fresh-context reviewers: Milestone review A after M0, Milestone review B after the permissions and API tasks (Tasks 6 to 12 or 12-B), Milestone review C before each release. Each review section below says who is dispatched and what they get.

**Goal:** Ship `Umbraco.Community.ArchivedUsers` v0.1 ("Archive by hand": F1 to F8 plus the D6 hint) as `18.0.0-alpha.1` on `main`, then port it to the `v17` branch as `17.0.0-alpha.1`.

**Architecture:** One Razor Class Library holds a line-agnostic `Core/` folder (settings, NPoco tables, migration, repository, service, notifications, handlers) and a per-line `Web/` folder (Management API controllers, the `/filter/user` action filter, OpenAPI wiring, composer). A Lit + Vite client in `Client/` builds into the committed `wwwroot/`, served at `/App_Plugins/ArchivedUsers/`. All user state changes go through `IUserService`; the package only owns its two tables.

**Tech Stack:** .NET 10, Umbraco CMS 18.3+ (17.8+ on `v17`), NPoco package migrations, xUnit + NSubstitute (unit), NUnit + `Umbraco.Cms.Tests.Integration` (integration and API), TypeScript + Lit + Vite, `@hey-api/openapi-ts`, Vitest (client logic), Playwright + `@umbraco/playwright-testhelpers` + `@axe-core/playwright` (E2E).

**Spec:** `docs/design.md` (copied from Pascal's private workspace in Task 1). Background: the private scope notes and portfolio README. Security: the private security review (findings AU-01 to AU-16, tests T1 to T21, UNKNOWNs U1 to U5); the design's "Security review changes (2026-10-30)" section says what was applied. Revised 2026-10-30 to match, and again after the plan review (see "Plan review changes (2026-10-30)" at the end). `security-review.md` and `plan-review.md` are private: they are never copied into the public repo.

**Repo:** `~/Projects/Umbraco.Community.ArchivedUsers` (clone of `PascalA94/Umbraco.Community.ArchivedUsers`, made **public** at the end of Task 1). Structure mirrors `~/Projects/Umbraco.Community.FormsAuditTrail`, which is read-only reference.

## Global Constraints

- **Version floors (spec, "Resolved by Pascal", 2026-10-30):** `main` uses `Umbraco.Cms.Web.Common` and `Umbraco.Cms.Api.Management`, both `[18.3.0, 19.0.0)` (P12e: the controller base, route attribute and `PagedViewModel` live in `Api.Management` / `Api.Common`), and npm `@umbraco-cms/backoffice` `^18.3.0`. `v17` uses `[17.8.0, 18.0.0)` and `^17.8.0`. CI tests the floor (`18.3.0` / `17.8.0`) and the latest minor (`18.*` / `17.*`). Why: core refuses disabled users in the authorize flow from these releases, and they clear the 27 Oct 2026 advisories (AU-02). Never lower them without re-opening AU-02.
- **Package versions:** `18.0.0-alpha.N` on `main`, `17.0.0-alpha.N` on `v17`. A bump touches the csproj `<Version>`, `Client/package.json`, `Client/package-lock.json` and `Client/public/umbraco-package.json` together (Forms Audit Trail rule).
- **Toolchain:** `net10.0`, .NET 10 SDK, Node 24.13+ via `Client/.nvmrc`.
- **Permanent names (never rename after release):** NuGet ID `Umbraco.Community.ArchivedUsers`; migration plan name `Umbraco.Community.ArchivedUsers`; migration state `archived-users-1`; tables `archivedUsersUser`, `archivedUsersAudit` and their columns; route `archived-users`; OpenAPI group `Archived Users`; every client alias starts `ArchivedUsers.` and matches the spec's section 7.3 table exactly.
- **S1 [Critical] backoffice authorisation:** every controller inherits `ManagementApiControllerBase` and carries `[VersionedApiBackOfficeRoute("archived-users")]`, `[Authorize(Policy = AuthorizationPolicies.SectionAccessUsers)]` and `[ApiExplorerSettings(GroupName = "Archived Users")]`. Never `UmbracoApiController`, plain `ControllerBase` or `[AllowAnonymous]`. A reflection test enforces this (Task 11).
- **P2 [Critical] no statics holding scoped things:** service, repository and list filter are DI-registered as scoped; the filter is added with `MvcOptions.Filters.AddService<ArchivedUserListFilter>()`. No static `IScope`, `HttpContext`, `IUser` or settings snapshot.
- **S3 [Critical] XSS (AU-04):** reason and names are rendered only through Lit's default escaping. User data never goes into a string passed to `UMB_CONFIRM_MODAL`, `localize.htmlString`, `localize.term` used as HTML, or notification `peek` HTML; pass it as a `TemplateResult` with text bindings or as localisation args. The package never uses `UMB_CONFIRM_MODAL`. `localize.htmlString` is banned outright, and the first argument of `localize.term` is always a string literal (P11). `build/check-xss.sh` (Task 13, run in CI) fails on `unsafeHTML`, `unsafeSVG`, `unsafeStatic`, `innerHTML`, `outerHTML`, `insertAdjacentHTML`, `DOMParser`, `createContextualFragment`, `document.write`, `setHTMLUnsafe`, `srcdoc`, `eval(`, `new Function(`, `UMB_CONFIRM_MODAL`, `htmlString` and a non-literal `.term(` key anywhere in `Client/src`. Server trims `reason`; empty after trim becomes `null`; over 500 chars is a 400.
- **P5 / S5 [Critical]:** the package ships no Razor views and no `SurfaceController`. The demo site adds none.
- **D1 to D4 [data integrity]:** not applicable (no document types). The equivalent rule here: migrations are additive only.
- **No privilege widening (AU-01):** `IUserService` does **not** authorise (`DisableAsync` has no admin check, `EnableAsync` has none at all). `ArchivedUserService` is the single choke point: every write passes, per target, `IUserPermissionService.AuthorizeAccessAsync(performer, { key })` **and** `UserVisibility.CanSee`, and the superuser is never a target. Failure is `NotPermitted`, and core is never called. A disabled or missing performer gets `NotPermitted` for every key (design 7.2 archive step 0, P9). Writes to users only via `IUserService.DisableAsync` / `EnableAsync`, one user per call, plus `IBackOfficeUserManager.UpdateSecurityStampAsync` (AU-02). User reads only via `IUserService.FilterAsync` or `IUserService` reads followed by `UserVisibility.CanSee`. No SQL against `umbracoUser`, no FK to `umbracoUser`. Non-admins get `NotPermitted` for unknown keys too (AU-16).
- **Request hygiene (AU-12, AU-14):** request models carry only `UserIds`, `Reason`, `Enable`; performer, source and times are derived on the server, the performer only from `CurrentUserKey(IBackOfficeSecurityAccessor)`. `[FromBody]` plus `[Consumes("application/json")]` (form posts get 415). No state change on GET. NPoco parameterised `Sql<ISqlContext>` only; `orderBy` is an enum.
- **Logging (AU-09, AU-12):** log user keys only. Never names, emails or `reason`. No user text in a message template.
- **`/filter/user` filter fails open (AU-10):** any exception in the package branch logs and calls `next()`. Hides only archived **and** disabled users (AU-03).
- **Supply chain (AU-15):** `@umbraco-cms/backoffice` is a `devDependency` and a Vite external; `package-lock.json` and `packages.lock.json` committed; CI uses `npm ci --ignore-scripts`; every GitHub Action pinned to a commit SHA with the tag in a comment.
- **Locked restore only at the floor (P1):** lock files are generated and committed at the floor versions (the defaults of the MSBuild properties `UmbracoTestVersion` and `UmbracoDemoVersion`). The floor CI leg runs `dotnet restore --locked-mode`. A latest-minor leg (`18.*` / `17.*`) passes the property and restores with `--force-evaluate -p:RestoreLockedMode=false`; its rewritten lock files are never committed. Only the floor leg checks `git diff --exit-code -- '**/packages.lock.json'`.
- **Public repository (Pascal, 2026-10-30):** the repo is public from the end of Task 1. Its `docs/` holds `design.md`, `plan.md` and `spikes.md` only. `security-review.md`, `plan-review.md`, milestone review reports and anything about an undisclosed vulnerability stay in Pascal's private workspace (private). PR checks run in the repo's own `build.yml` from Task 1: gitleaks, osv-scanner and `build/check-vulnerable.sh`. No organisation enrolment.
- **Same-user concurrency (P6):** archive and unarchive hold the target's lock from `ArchiveUserLocks` (DI singleton, 64 striped `SemaphoreSlim(1,1)`, never a static) from the state read to the record write. The lock is not re-entrant: a `UserArchivingNotification` handler must not call the service for the same user.
- **API paging bounds (P14):** `GET archived-users` takes `skip >= 0` and `take` 0 to 1,000 (default 100); otherwise 400. `take = 0` returns no items and the filtered total.
- **GDPR (D7):** package tables store user keys, never emails. The audit stores a name snapshot only.
- **Enums stored as strings.** Times stored and returned as UTC; reads set `DateTimeKind.Utc` because SQLite loses it.
- **Hide switch off = core.** With `HideArchivedFromUserList: false` the list filter calls `next()` and touches nothing.
- **Request limit:** at most 500 user ids per archive or unarchive call, else 400.
- **Client:** use exported condition constants (`UMB_COLLECTION_ALIAS_CONDITION`, `UMB_IN_MODAL_CONDITION_ALIAS`, `UMB_WORKSPACE_CONDITION_ALIAS`), not strings. All labels via localisation (`en` and `en-GB`, en-GB is the default copy). Reuse UUI and core kinds.
- **`Core/` is line-agnostic:** nothing in `src/Umbraco.Community.ArchivedUsers/Core/` may reference `Filter`/`Filter2`, Swashbuckle or `Microsoft.AspNetCore.OpenApi`. Task 19 proves the folder is identical on both branches.
- **Committed `wwwroot/`:** rebuild the client and commit `wwwroot/` with any `Client/` change. CI fails on drift.
- **Secrets:** demo admin and demo user passwords live in `dotnet user-secrets`, never in `appsettings*.json`.
- **Harness:** Claude never runs `git commit` (a hook denies it). Every commit is a checkpoint Pascal runs. Suggested messages carry no AI attribution. Git identity is already set; don't change it. After Task 1, work happens on branch `feat/v0.1` and reaches `main` by PR (the ruleset requires it).
- **Gating versus recording tests (P3):** a core contract test marked `[Category("Gating")]` is never edited to match what core does; its failure triggers the named fallback. `[Category("Tripwire")]` failures go to `security-analyst`. Only `[Category("Recording")]` tests may be updated to assert the observed value, with the observation written in `docs/spikes.md`.
- **Comments explain why.** Every deliberate deviation from core behaviour carries a why-comment that cites the spec section or `docs/spikes.md`.

## Review Focus

1. **The same user archived twice at once** (double-click, two admins, client retry): the second call gets `AlreadyArchived`, exactly one record and one `Archived` audit row exist, `WasDisabledBefore` is false for a user who was enabled, no 500. Pinned in Task 5 (`TryInsertAsync` duplicate), Task 7 (`ArchiveUserLocks`, the collision path and the real-DB concurrency test on SQLite and SQL Server) and Task 11 (two parallel POSTs).
2. **Hostile or odd reason text** (`<img src=x onerror=alert(1)>`, whitespace only, 501 chars, emoji and non-Latin script): stored trimmed and verbatim, rendered as text, whitespace-only counts as empty for `RequireReason`, 501 chars is a 400. Pinned in Task 11 (`ReasonValidator`) and Task 16 (rendering).
3. **An archive record whose user was deleted or renamed outside the package** (SQL, uSync, a missed notification): the Archived view skips the orphan instead of failing, and a renamed user shows the current name. Pinned in Task 9.
4. **Paging edges on `/filter/user`:** `skip` at or past the filtered total, `take = 0`, a partial last page, and a page whose core slice is all archived all return the right items and the filtered `total`. Pinned in Task 12 (`UserListPaging`).
5. **The archiving admin is later deleted:** `archivedBy` returns `{ id, name: null }` and the UI shows "Removed user", never a blank or a crash. Pinned in Task 9 and Task 15.

Security review items (T1 to T21) are mapped to tasks in "Security review coverage" at the end of this plan.

## Milestone reviews (hybrid execution)

Three review points, from the portfolio README "Build approach": A after M0, B after the permissions and API tasks, C before each release. The rules for all three:
- **Fresh context.** Each reviewer is a new subagent that did not write the code. It gets the code, the diff range and the spec, never the implementer's summary or conclusions.
- **Who.** `qa-reviewer` (model `sonnet`) every time. Review B adds `security-analyst` (model `opus`), dispatched separately in parallel, because Tasks 6 to 12 carry AU-01 to AU-04 and AU-10. `security-analyst` also gets the private `security-review.md` from the workspace folder.
- **Output.** Each reviewer returns JSON findings (id, severity, file and line, issue, fix). The lead session saves each verdict as `<A|B|C>-<reviewer>-<date>.json` in Pascal's **private** workspace, never in the repo (the repo is public, and findings may describe unfixed weaknesses).
- **Blocking.** Critical and High findings block the next milestone. A `security-analyst` Critical is never waived or argued down. Fixes go in the owning task's files under TDD, then the same reviewer re-checks the blocking findings once. Medium and lower go to Pascal as advisories.
- **Pascal decides** anything a reviewer flags as a spec change; it lands in `design.md` first.

---

## File structure (end state on `main`)

```
~/Projects/Umbraco.Community.ArchivedUsers/
  .github/workflows/build.yml, nightly.yml, publish.yml
  .github/dependabot.yml
  build/check-vulnerable.sh, build/check-xss.sh, build/gitleaks.sh
  Directory.Build.props                                   (lock files, UmbracoTestVersion, UmbracoDemoVersion)
  docs/design.md, docs/plan.md, docs/spikes.md          (public; never security-review.md or plan-review.md)
  demo/Umbraco.Community.ArchivedUsers.DemoSite/        (not packed; Seed/DemoUserSeeder.cs)
  src/Umbraco.Community.ArchivedUsers.slnx
  src/Umbraco.Community.ArchivedUsers/                  (RCL, the nupkg)
    Core/Configuration/ArchivedUsersSettings.cs
    Core/Models/ArchiveSource.cs, ArchiveAuditAction.cs, ArchiveResultStatus.cs,
               ArchivedUserRecord.cs, ArchiveAuditEntry.cs, UserArchiveResult.cs,
               CleanUpPreview.cs, ArchivedUserView.cs, ArchivedUserOrder.cs
    Core/Notifications/UserArchiveEvent.cs, UserArchivingNotification.cs,
                       UserArchivedNotification.cs, UserUnarchivedNotification.cs
    Core/Persistence/ArchivedUserDto.cs, ArchiveAuditDto.cs,
                     IArchivedUserRepository.cs, ArchivedUserRepository.cs
    Core/Migrations/ArchivedUsersMigrationPlan.cs, AddArchivedUserTables.cs
    Core/Security/UserVisibility.cs
    Core/Services/IArchivedUserService.cs, ArchivedUserService.cs, ArchiveUserLocks.cs
    Core/Handlers/UserSavedArchiveSyncHandler.cs, UserDeletedArchiveCleanupHandler.cs,
                  ReconcileOnStartupHandler.cs
    Core/ArchivedUsersBuilderExtensions.cs
    Web/Controllers/ArchivedUsersControllerBase.cs, ArchivedUsersQueryController.cs,
                    ArchivedUsersCommandController.cs, Models/*.cs, ReasonValidator.cs
    Web/Filters/ArchivedUserListFilter.cs, UserListPaging.cs, ArchivedUserListWarningClock.cs
    Web/Controllers/ArchivedUsersUserListController.cs  (Option B only, Task 12-B)
    Web/OpenApi/ArchivedUsersOpenApi.cs
    Web/ArchivedUsersComposer.cs
    Client/  (package.json, .nvmrc, tsconfig.json, vite.config.ts, vitest.config.ts,
              openapi-ts.config.ts, public/umbraco-package.json, src/**, test/**)
    wwwroot/ (built client, committed)
  src/Umbraco.Community.ArchivedUsers.Tests/            (xUnit + NSubstitute)
  src/Umbraco.Community.ArchivedUsers.IntegrationTests/ (NUnit + Umbraco.Cms.Tests.Integration)
  tests/e2e/                                            (Playwright, incl. specs/xss.spec.ts, specs/session.spec.ts)
  umbraco-marketplace.json, README.md, LICENSE, SECURITY.md, CLAUDE.md, .gitignore, .gitattributes
```

Why one RCL rather than the spec's suggested Core / Web / Client projects: it matches Forms Audit Trail, ships one nupkg with no pack tricks, and the spec leaves the split to the builder (section 12). The `Core/` folder rule above keeps the "identical on both branches" property.

---

## M0: scaffold and go/no-go spikes

### Task 1: Clone and scaffold the repository, CI and PR checks from day one

**Files:**
- Create: everything in the file structure above except the `Core/`, `Web/` and `Client/src/**` contents, `IntegrationTests/`, `tests/e2e/`, `nightly.yml` and `build/check-xss.sh`.
- Test: `src/Umbraco.Community.ArchivedUsers.Tests/PackageManifestTests.cs`

**Interfaces:**
- Produces: solution `src/Umbraco.Community.ArchivedUsers.slnx`; RCL with `StaticWebAssetBasePath` `App_Plugins/ArchivedUsers`, `InternalsVisibleTo` both test projects; client bundle entry `Client/src/index.ts` exporting `manifests: UmbExtensionManifest[]` (empty for now); demo site project at `demo/Umbraco.Community.ArchivedUsers.DemoSite` with a `ProjectReference` to the RCL.
- Produces: `Directory.Build.props` with `RestorePackagesWithLockFile=true`, `UmbracoTestVersion` (default `18.3.0`, used from Task 3) and `UmbracoDemoVersion` (default `18.3.0`, the demo site's `Umbraco.Cms` version). Later tasks and CI legs override these two properties; nothing else does (P1).
- Produces: `build.yml` jobs `build` (matrix leg `floor` only for now), `secrets`, `deps`; `build/check-vulnerable.sh`; `build/gitleaks.sh`. Tasks 3, 5, 13 and 17 add steps to these jobs.

- [ ] **Step 1: Clone.** `git clone git@github.com:PascalA94/Umbraco.Community.ArchivedUsers.git ~/Projects/Umbraco.Community.ArchivedUsers`. Expected: "warning: You appear to have cloned an empty repository." Run `git config user.email` and confirm it is `5427750+PascalA94@users.noreply.github.com`. Don't change it.

- [ ] **Step 2: Copy the public docs.** Copy **only** `design.md` and `plan.md` from Pascal's private workspace into `docs/`. Never copy `security-review.md`, `plan-review.md`, `scope.md` or any `reviews/` file (Pascal, 2026-10-30: the repo is public). Create `docs/spikes.md` with one heading per spike (S0 to S6) and per security UNKNOWN (U1 to U5). Under each heading, two lines in exactly this form so Task 4a Step 5 can grep them:
  - `**Gate:** ` (left empty; S0, S1, S2, S4, S6 only), later `GO` or `NO-GO (fallback: <name>)`. S5's line starts as `**Gate:** pending (Task 5 Step 5)`, so the M0 exit check doesn't count it.
  - `**Result / Decision:** ` (left empty), later the result with at least one source link or test name.
  - For U1 to U5, also the "Pass (closed) when" text from the design's section 8 table.
  - A top line: "Public file. Never record details of an undisclosed vulnerability here; those go to Pascal's private workspace."

- [ ] **Step 3: Scaffold from Forms Audit Trail.** Copy and rename from `~/Projects/Umbraco.Community.FormsAuditTrail` (read only): `.gitignore`, `.gitattributes`, `LICENSE` (MIT, Pascal Meyer), `SECURITY.md`, `src/*.slnx`, the RCL csproj, the tests csproj, `Client/{package.json,.nvmrc,tsconfig.json,vite.config.ts,public/umbraco-package.json}`, `.github/workflows/{build,publish}.yml`. Changes:
  - csproj: drop the Forms and EF Core references; reference `Umbraco.Cms.Web.Common` **and** `Umbraco.Cms.Api.Management`, both `[18.3.0, 19.0.0)` (P12e; S6 and S1 confirm no other core package is needed, and Task 2 Step 7 records it); `<Version>18.0.0-alpha.1</Version>`; `StaticWebAssetBasePath` `App_Plugins/ArchivedUsers`; description from spec section 1 intent; tags `umbraco;umbraco-marketplace;users;security;governance`; repo URLs to the new repo.
  - `Directory.Build.props` at the repo root: `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>`, `<UmbracoTestVersion Condition="'$(UmbracoTestVersion)' == ''">18.3.0</UmbracoTestVersion>`, `<UmbracoDemoVersion Condition="'$(UmbracoDemoVersion)' == ''">18.3.0</UmbracoDemoVersion>`, and `<DefineConstants>$(DefineConstants);UMBRACO_HAS_API_USERS</DefineConstants>` (P13a; Task 19 removes it on `v17` if 17.8 has no `UserKind.Api`). Why-comment: lock files describe the floor; latest-minor legs restore unlocked (P1).
  - `umbraco-package.json`: name `Umbraco.Community.ArchivedUsers`, version `18.0.0-alpha.1`, one extension `{ "type": "bundle", "alias": "ArchivedUsers.Bundle", "name": "Archived Users Bundle", "js": "/App_Plugins/ArchivedUsers/index.js" }`.
  - `package.json`: name `umbraco-community-archived-users-client`, version `18.0.0-alpha.1`, `@umbraco-cms/backoffice ^18.3.0` under **`devDependencies`**, add `vitest` and `happy-dom` and script `"test": "vitest run"`. `vite.config.ts`: `build.rollupOptions.external: [/^@umbraco-cms\/backoffice/]` (AU-15: never bundle core client code).
  - `.github/dependabot.yml`: weekly updates for `nuget` (`/src`), `npm` (`/src/Umbraco.Community.ArchivedUsers/Client`, `/tests/e2e`) and `github-actions` (`/`) (AU-15).
  - `.gitignore`: add `demo/**/umbraco/Data/`, `demo/**/umbraco/Logs/`, `demo/**/wwwroot/media/`, `tests/e2e/node_modules/`, `tests/e2e/test-results/`, `artifacts/`.

- [ ] **Step 4: CI and PR checks in `build.yml`** (Pascal's decision: the repo's own CI, no enrolment). Triggers: pull requests to `main` and `v17`, and pushes to both. Every `uses:` pinned to a full commit SHA with the tag in a trailing comment. Three jobs:
  - `build`: matrix `leg: [floor]` for now (Task 3 adds `latest`). `npm ci --ignore-scripts` (if a build tool needs its install script, allow-list it and comment why); client build; `npm test`; **`git diff --exit-code -- src/Umbraco.Community.ArchivedUsers/wwwroot`** (P13c: committed bundle matches a fresh build); a step that fails if `wwwroot/index.js` contains no bare `@umbraco-cms/backoffice/` import (proves the external held); `dotnet restore --locked-mode` on the floor leg; `dotnet build -c Release --no-restore`; `dotnet test -c Release --no-build`.
  - `secrets`: `actions/checkout` with `fetch-depth: 0`, then `bash build/gitleaks.sh "${{ github.event.pull_request.base.sha || github.event.before }}..${{ github.sha }}"`. `build/gitleaks.sh` downloads a pinned gitleaks release tarball, checks its SHA-256 against a value in the script, and runs `gitleaks git --redact --exit-code 1 --log-opts="$1"`. Why the CLI and not `gitleaks-action`: no licence key needed if the repo later moves to an organisation.
  - `deps`: `google/osv-scanner-action` (reusable workflow, SHA-pinned) over the repo's lock files (`packages.lock.json`, `package-lock.json`), failing on any finding; then `bash build/check-vulnerable.sh`.
  - `build/check-vulnerable.sh` (T21, AU-15): runs `dotnet list src/Umbraco.Community.ArchivedUsers.slnx package --vulnerable --include-transitive --format json`, exits 1 if any project in the JSON has a non-empty `vulnerabilities` array (never trusts the command's exit code).

- [ ] **Step 5: `umbraco-marketplace.json`.** Same shape as Forms Audit Trail. Title "Archived Users", Category "Editor Tools" (the value Forms Audit Trail had to fix to; the spec's "backoffice extension" isn't a valid category), Tags `["users", "security", "governance"]`, URLs to the new repo.

- [ ] **Step 6: Demo site.** `dotnet new install Umbraco.Templates::18.3.0`, then `dotnet new umbraco -n Umbraco.Community.ArchivedUsers.DemoSite -o demo/Umbraco.Community.ArchivedUsers.DemoSite --development-database-type SQLite`. Set its `Umbraco.Cms` package version to `$(UmbracoDemoVersion)`. Don't pass `--email`/`--password` (they land in a committed file). Set unattended install in `appsettings.Development.json` with name and email `admin@example.test` only; set the password with `dotnet user-secrets set "Umbraco:CMS:Unattended:UnattendedUserPassword" <value>`. Add the `ProjectReference` to the RCL and add the demo project to the slnx.

- [ ] **Step 7: Write the failing test.** `PackageManifestTests.Manifest_identity_is_permanent`: reads `src/Umbraco.Community.ArchivedUsers/wwwroot/umbraco-package.json` (path resolved from the test assembly location) and asserts `name == "Umbraco.Community.ArchivedUsers"`, exactly one extension, `type == "bundle"`, `alias == "ArchivedUsers.Bundle"`, `js == "/App_Plugins/ArchivedUsers/index.js"`.

- [ ] **Step 8: Run it.** `dotnet test src/Umbraco.Community.ArchivedUsers.slnx --filter PackageManifestTests`. Expected: FAIL, file not found (no `wwwroot/` yet).

- [ ] **Step 9: Build the client.** `Client/src/index.ts` exports `export const manifests: Array<UmbExtensionManifest> = [];`. Run `cd src/Umbraco.Community.ArchivedUsers/Client && npm install && npm run build`. Expected: `../wwwroot/index.js` and `../wwwroot/umbraco-package.json` exist.

- [ ] **Step 10: Verify.** `dotnet restore src/Umbraco.Community.ArchivedUsers.slnx && dotnet build src/Umbraco.Community.ArchivedUsers.slnx -c Release && dotnet test src/Umbraco.Community.ArchivedUsers.slnx -c Release`. Expected: "Build succeeded", "Passed!  - Failed: 0, Passed: 1", and a `packages.lock.json` beside each csproj. `bash build/check-vulnerable.sh` exits 0. Then `dotnet run --project demo/Umbraco.Community.ArchivedUsers.DemoSite`, log in at `/umbraco`, open DevTools Network: `/App_Plugins/ArchivedUsers/index.js` returns 200, console has no errors.

- [ ] **Step 11: Secret scan before anything is public.** `bash build/gitleaks.sh ""` on the working tree after `git add -A` (`gitleaks dir . --redact --exit-code 1` mode in the script when the argument is empty). Expected: "no leaks found". `git grep -nE "Password|ApiKey|Secret" -- ':!*.md'` shows only config key names, no values.

- [ ] **Checkpoint (Pascal commits):** `git add -A` / suggested message `chore: scaffold Archived Users package, demo site and CI`. Then `git push -u origin main`, `git switch -c feat/v0.1` and `git push -u origin feat/v0.1`.

- [ ] **Step 12 (Pascal): Go public and switch on GitHub protections.**
  - `gh repo edit PascalA94/Umbraco.Community.ArchivedUsers --visibility public --accept-visibility-change-consequences`.
  - Settings, Code security: secret scanning with push protection on; Dependabot alerts on; CodeQL default setup on (C# and JavaScript/TypeScript).
  - A branch ruleset on `main` and `v17`: require a pull request, require the `build`, `secrets` and `deps` checks, block force pushes and deletion.
  - Expected: `gh repo view --json visibility` prints `PUBLIC`; a draft PR from `feat/v0.1` shows the three checks green.

- [ ] **Step 13 (Pascal): Reserve the NuGet IDs** (spec section 10, "do first"). In a scratch folder outside the repo, `dotnet new classlib` twice with `PackageId` `Umbraco.Community.ArchivedUsers` and `Umbraco.Community.ArchivedUsers.Automate`, `Version` `0.0.1-alpha`, description "Reserved. Archive leavers in the Umbraco Users section. Coming soon.", MIT. `dotnet pack`, push from Pascal's NuGet account, then unlist both on nuget.org. Confirm 2FA is on for the NuGet and GitHub accounts (AU-15).

### Task 2: Spike S1 + S3, `/filter/user` hide at runtime (GO/NO-GO)

Time box: one day. Spike code lives on a branch `spike/s1-list-filter` and is not merged, except the demo seeder, which is kept.

**Files:**
- Create (kept): `demo/Umbraco.Community.ArchivedUsers.DemoSite/Seed/DemoUserSeeder.cs`, `Seed/DemoSeedComposer.cs`
- Create (spike only): `Web/Filters/SpikeListFilter.cs`
- Modify: `docs/spikes.md` (S1, S3 sections)

**Interfaces:**
- Produces: seeded demo users (below) that Tasks 12, 16 and 18 rely on; a GO or NO-GO decision recorded in `docs/spikes.md` that selects Task 12 (Option A) or Task 12-B (Option B).

- [ ] **Step 1: Demo seeder (kept).** `DemoUserSeeder : INotificationAsyncHandler<UmbracoApplicationStartedNotification>`, registered only when config `ArchivedUsersDemo:Seed` is `true` (set in `appsettings.Development.json`). Idempotent: does nothing if `demo.user01@example.test` exists. Uses only `IUserService` and `IUserGroupService`. Creates: user group `User managers` (alias `userManagers`, sections `Constants.Applications.Users`, `Content`); "Morgan Manager" (`morgan@example.test`, in `userManagers` and `editor`, not admin); a second admin "Alex Admin"; 30 enabled editors `demo.user01` to `demo.user30`; 8 disabled editors with a `LastLoginDate`; 2 disabled editors with none; 1 invited user. Passwords from user-secret `ArchivedUsersDemo:UserPassword`.

- [ ] **Step 2: Read core.** Read `FilterUserFilterController` at the `release-18.3.0` tag of Umbraco-CMS: record the action name (`Filter2` expected), its parameter names and types, the attribute route template, how it builds `UserFilter`, and which `IUserPresentationFactory` method it calls (sync or async). Paste the findings into `docs/spikes.md` S1 with the commit SHA.

- [ ] **Step 3: Spike filter.** On the spike branch, a global `IAsyncActionFilter` that matches `ControllerActionDescriptor.ControllerTypeInfo == typeof(FilterUserFilterController)`, reads `context.ActionArguments`, calls `FilterAsync(currentUserKey, filter, 0, int.MaxValue, orderBy, orderDirection)`, drops three hard-coded keys, pages in memory and sets `context.Result = new OkObjectResult(new PagedViewModel<UserResponseModel> { Total, Items })`. Archive three seeded users by disabling them in the backoffice and adding their keys to the spike filter.

- [ ] **Step 4: Run S1 on 18.3.x (floor) and latest 18.x.** For the latest run: `dotnet restore demo/Umbraco.Community.ArchivedUsers.DemoSite -p:UmbracoDemoVersion=18.* --force-evaluate -p:RestoreLockedMode=false`, then `git checkout -- '**/packages.lock.json'` after the run (P1: the floating version never reaches a committed lock file). Check: Users list total and page 2 (page size 10 to force paging); search by name; the user picker in a user group's "Add users" dialog; the Status filter set to Disabled. Watch the console for client errors.
  - GO if: the three users never appear, totals and paging are right, the client renders our payload with no console errors, and the match catches the action on both versions.
  - NO-GO if any of those fail.

- [ ] **Step 5: Run S1 on 17.8.x** in a scratch Umbraco 17.8 site outside the repo (same filter against `Filter`, no `userKinds`). Record the result for Task 19. It does not change the `main` gate.

- [ ] **Step 6: S3 and U5.** In DevTools Network, record which endpoint each surface calls: Users collection, user group "Add users" picker, user editor, content "created by" info, user group member list, current user, **and every non-Users-section surface that can list users: the user-picker property editor on a document type in Content, and any Settings or Members screen with a user picker** (U5). U5 passes when every surface that calls `/filter/user` is listed in `docs/spikes.md` U5; that list goes into the README's D1 section (Task 17). Then set `Umbraco:CMS:Security:HideDisabledUsersInBackOffice: true` and record whether `FilterAsync` with `IncludeUserStates = { Disabled }` returns any users. Write both into `docs/spikes.md` S3. S3's `**Result / Decision:**` line must name exactly one of `FilterAsync` or `paged IUserService reads` for Task 8 (P4: an objective decision, not "recorded"). U5's line lists each surface with its endpoint, and says for each whether it hides archived users under Option A and under Option B (Task 12-B relies on this).

- [ ] **Step 7: Decide.** In `docs/spikes.md` S1 set `**Gate:** GO` (build Task 12, Option A) or `**Gate:** NO-GO (fallback: Task 12-B, Option B)`, with the evidence. Also record which core assemblies the spike filter needed (`FilterUserFilterController`, `PagedViewModel`, `IUserPresentationFactory`) and confirm the Task 1 package references cover them (P12e). Delete the spike branch's filter; keep the seeder.

- [ ] **Checkpoint (Pascal commits):** `git add demo/ docs/spikes.md` / suggested message `chore: seed demo users and record spikes S1 and S3`.

### Task 3: Spike S0 + S2 + S4, integration harness and core contract tests (GO/NO-GO)

Time box: one day. These tests are kept: they fail loudly when a core minor changes the behaviour the package depends on.

**Files:**
- Create: `src/Umbraco.Community.ArchivedUsers.IntegrationTests/Umbraco.Community.ArchivedUsers.IntegrationTests.csproj`, `TestUsers.cs`, `HarnessTests.cs`, `CoreContractTests.cs`
- Modify: `src/Umbraco.Community.ArchivedUsers.slnx`, `.github/workflows/build.yml` (integration tests and the `latest` leg), `docs/spikes.md`

**Interfaces:**
- Produces: `TestUsers` helper used by every later integration test:
  - `Task<IUser> CreateAsync(string name, string[] groupAliases, UserState state = UserState.Active, bool hasLoggedIn = true)`
  - `Task<IUserGroup> EnsureUserManagersGroupAsync()` (alias `userManagers`, sections Users and Content)
  - `Task<IUser> CreateUserManagerAsync()` (non-admin with Users section access)
  - `Task<IUser> CreateApiUserAsync(string name, string clientId, string secret)` (v18 `UserKind.Api` with a client credential, inside `#if UMBRACO_HAS_API_USERS`), used by this task's T5 baseline, Task 11 (T5) and Task 12.
  - `Task<HttpClient> CreateAuthenticatedClientAsync(IUser user)` (a backoffice-authenticated `HttpClient` for API tests; how it authenticates is S0's result)
- Produces: the `UmbracoTestVersion` property (Task 1) consumed by the test csproj, and the CI `latest` leg.
- Produces: the S4 notes Task 7 needs: the exact `IUserPermissionService.AuthorizeAccessAsync` signature and result type, the `IAuditService` write call and `AuditType` to use, and how `IBackOfficeUserManager` resolves a user by key.
- Produces: NUnit categories `Gating`, `Tripwire`, `Recording` on every `CoreContractTests` test (P3, see Global Constraints).

- [ ] **Step 1 (S0): Write the harness tests.** NUnit test project referencing `Umbraco.Cms.Tests.Integration` `$(UmbracoTestVersion)`, `Microsoft.NET.Test.Sdk`, `NUnit`, `NUnit3TestAdapter`, and the RCL. Service tests inherit `UmbracoIntegrationTest` with `[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]`; HTTP tests inherit `UmbracoTestServerTestBase` (or `ManagementApiTest<T>`, whichever the package exposes publicly).
  - `HarnessTests.Harness_boots_and_resolves_IUserService`: `GetRequiredService<IUserService>()` is not null.
  - `HarnessTests.Harness_authenticates_backoffice_user_over_http` (P2): create an admin with `TestUsers`, get a client from `CreateAuthenticatedClientAsync`, `GET /umbraco/management/api/v1/user/current` returns 200 with that user's id; the same request without auth returns 401.
- [ ] **Step 2: Run** `dotnet test src/Umbraco.Community.ArchivedUsers.IntegrationTests --filter HarnessTests`. Expected: both PASS on SQLite. **S0 gate:** GO only when both pass. Record in `docs/spikes.md` S0 the base class used, how `CreateAuthenticatedClientAsync` authenticates, the back-office token endpoint path (Task 16 `session.spec.ts` uses it), and the method `MigrationTests` will use to run a package migration plan (`IPackageMigrationRunner` or `MigrationPlanExecutor`).
  - **S0 NO-GO fallback (P2):** the test project hosts the demo site with `WebApplicationFactory<Program>` (the demo site's `Program` made visible with `public partial class Program`), one isolated SQLite file per fixture under the test's temp directory, and unattended install from config overrides in `WithWebHostBuilder`. Service tests resolve services from `factory.Services` inside `CreateScope()`, so the service-level tests (`ICoreScope` deferral, `Repository_without_scope_throws`, drop-table fail-open, parallel reconcile) still run. `CustomTestSetup` becomes `WithWebHostBuilder(b => b.UseSetting(...))`. `CreateAuthenticatedClientAsync` signs in through the back-office login endpoint and runs the authorization-code-with-PKCE flow with an `HttpClient` that keeps cookies. What the fallback can't cover, written into `docs/spikes.md` S0: a fresh schema per test (tests clean up after themselves instead), the test framework's builders (use `IUserService` directly), and the SQL Server leg through the framework's `Tests:Database` settings (the nightly job passes its own connection string instead).
- [ ] **Step 3 (S2): Write the gating contract tests** (`[Category("Gating")]`).
  - `CoreContractTests.EnableAsync_publishes_UserSavedNotification`: register a test handler that records `UserSavedNotification` entities; disable then `EnableAsync(adminKey, {key})`; assert the handler saw the user once, with `UserState != Disabled`. Fallback on failure: Task 10 builds `CoreEnableActionFilter` (Task 10 Step 3).
  - `CoreContractTests.EnableAsync_inside_outer_scope_defers_UserSaved_until_outer_complete`: open an `ICoreScope`, call `EnableAsync`, assert the handler has not run yet; `Complete()` and dispose; assert it ran once. (Task 9's unarchive flow relies on this, design 7.2.) Fallback on failure: Task 9 Step 4's `UnarchiveInProgress` marker (design 7.2 unarchive flow, P3).
- [ ] **Step 4 (S4, AU-01, AU-02): Write the remaining gating and tripwire tests.**
  - `AuthorizeAccessAsync_refuses_non_admin_on_admin` (`Gating`): `IUserPermissionService.AuthorizeAccessAsync(userManager, { adminKey })` is not success; with an admin performer it is success. Records the signature and result type. Fallback on failure: Task 7 keeps `UserVisibility.CanSee` as the only per-user rule (it already refuses non-admin on admin), and the failure goes to `security-analyst` before Task 7 starts.
  - `UpdateSecurityStampAsync_changes_stamp_without_HttpContext` (`Gating`, AU-02): resolve `IBackOfficeUserManager` in a scope with no `HttpContext`, find the user by key, call `UpdateSecurityStampAsync`; assert the stamp changed. Fallback on failure (design S4): set a new `IUser.SecurityStamp` and save through `IUserService`, recorded in `docs/spikes.md`.
  - `DisableAsync_does_not_check_admin` (`Tripwire`, T2, AU-01): a non-admin user manager performer disables an admin → `Success`, and the admin's `IsApproved` is false. Why-comment: if this ever fails, core added the check; nothing in the package depends on core's check, so the package's own check stays.
  - `EnableAsync_does_not_check_admin` (`Tripwire`, T2): the same with `EnableAsync` → `Success`.
  - `FilterAsync_hides_admins_from_non_admin` (`Tripwire`; proves the rule `UserVisibility` mirrors).
- [ ] **Step 5 (S4, U3, T5): Write the recording contract tests** (`[Category("Recording")]`). Each asserts the exact observed behaviour and records it in `docs/spikes.md`:
  - `DisableAsync_already_disabled_user` (expected `Success`; if not, Task 7 must not call core for disabled users, which it already avoids)
  - `DisableAsync_self` → `CannotDisableSelf`
  - `DisableAsync_invited_user` → `CannotDisableInvitedUser`
  - `DisableAsync_with_SuperUserKey_as_performer_succeeds_for_every_user_type` (editor, admin, user manager)
  - `DisableAsync_rotates_security_stamp` (U3): read `SecurityStamp` before and after `DisableAsync`; assert and record whichever is observed. U3 closes when the answer is recorded; the package rotates either way (Task 7).
  - `AuditService_write_is_readable_back` (AU-07): write one entry with the call S4 chose (expected `IAuditService.AddAsync` with `AuditType.Custom`, entity type `"User"`, comment `"ArchivedUsers: Archived (Manual)"`); read it back. Records the call for Task 7.
  - `Api_user_obtains_client_credentials_token` (P12f, T5 baseline, inside `#if UMBRACO_HAS_API_USERS`): `CreateApiUserAsync`, then `POST` the client-credentials token request; 200 with an `access_token`. Records the token endpoint and grant parameters Task 11's T5 test uses.
- [ ] **Step 6: Run.** `dotnet test src/Umbraco.Community.ArchivedUsers.IntegrationTests -c Release`, then the latest leg: `dotnet restore src/Umbraco.Community.ArchivedUsers.IntegrationTests -p:UmbracoTestVersion=18.* --force-evaluate -p:RestoreLockedMode=false && dotnet test src/Umbraco.Community.ArchivedUsers.IntegrationTests -c Release --no-restore -p:UmbracoTestVersion=18.*`, then `git checkout -- '**/packages.lock.json'`. Expected on both:
  - `--filter TestCategory=Gating`: all pass. A failure is never fixed by editing the test: write `**Gate:** NO-GO (fallback: <the fallback named on that test>)` in `docs/spikes.md` and apply that fallback in the task it names.
  - `--filter TestCategory=Tripwire`: all pass. A failure goes to `security-analyst` before Task 6 starts and is never silently updated.
  - `--filter TestCategory=Recording`: all pass, or the test is updated to assert the observed value and the observation is written in `docs/spikes.md`.
- [ ] **Step 7: CI.** In `build.yml`'s `build` job: add `latest` to the matrix (`leg: [floor, latest]`); the floor leg keeps `--locked-mode` and adds `git diff --exit-code -- '**/packages.lock.json'`; the latest leg restores with `-p:UmbracoTestVersion=18.* --force-evaluate -p:RestoreLockedMode=false` and builds and tests with the same property (P1); both legs run the integration tests. Expected on the PR: both legs green.
- [ ] **Step 8: Decide.** In `docs/spikes.md`: S0 `**Gate:**` GO, or NO-GO with the fallback; S2 `**Gate:**` GO (Task 10 uses `UserSavedNotification`, Task 9 relies on deferral), or NO-GO with the named fallback for each failing gating test; S4 `**Gate:**` GO when its gating tests pass, plus the per-user status map, the `AuthorizeAccessAsync` call, the stamp rotation route and the core audit call for Task 7; U3's `**Result / Decision:**` the stamp answer.
- [ ] **Checkpoint (Pascal commits):** `git add src/Umbraco.Community.ArchivedUsers.IntegrationTests src/*.slnx .github/workflows/build.yml docs/spikes.md` / suggested message `test: add integration harness and core contract tests (spikes S0, S2, S4)`.

### Task 4: Spike S6, frontend wiring and the OpenAPI client (GO/NO-GO)

Time box: half a day. Spike branch `spike/s6-frontend`, not merged; findings only.

**Files:**
- Modify: `docs/spikes.md` (S6)

- [ ] **Step 1:** Load the `umbraco-cms-backoffice-skills:umbraco-entity-actions`, `umbraco-entity-bulk-actions`, `umbraco-collection-action`, `umbraco-menu-items` and `umbraco-workspace` skills for manifest shapes.
- [ ] **Step 2:** Register throwaway versions of: an `entityAction` for `user` that shows in the user editor; an `entityBulkAction` on `Umb.Collection.User`; a `collectionAction` of kind `button` with an `api` next to Create, hidden in modals; a `menuItem` in `Umb.Menu.UserManagement` at weight 190 with a `default` workspace; a `collectionView` of kind `table` (if no such kind exists, record that the view will be a custom element using `umb-table`). Confirm each appears where the spec says, with no console errors.
- [ ] **Step 3:** Find which event core's Disable action dispatches to refresh the user editor and the collection (look at `Umb.EntityAction.User.Disable` and `Umb.EntityBulkAction.User.Disable`). Record the event classes and the context they go through. Also record how `UMB_NOTIFICATION_CONTEXT.peek` renders `data.message`: as text, or through `htmlString`/`unsafeHTML` (AU-04). If it renders HTML, Task 13 passes user text only as a `TemplateResult` or not at all.
- [ ] **Step 4:** Confirm a `condition` can re-evaluate when a global context's observable changes (needed for Archive/Unarchive toggling after an action).
- [ ] **Step 5: OpenAPI.** Find how a package registers its own document on v18 (`Microsoft.AspNetCore.OpenApi`): the registration call, the served JSON path, and how the operation ids come out. Generate a client with `@hey-api/openapi-ts` from a one-endpoint throwaway controller. NO-GO fallback: hand-write typed fetch functions in `Client/src/api/archived-users.api.ts` using `UMB_AUTH_CONTEXT` for the token. Record which.
- [ ] **Step 6: Verify.** `docs/spikes.md` S6 lists, for each item, "works as spec" or the replacement pattern, and sets `**Gate:** GO` when every item works as spec or has a named replacement, else `NO-GO (fallback: <replacement>)`. Expected: no "unknown" left.
- [ ] **Checkpoint (Pascal commits):** `git add docs/spikes.md` / suggested message `docs: record spike S6 frontend and OpenAPI findings`.

### Task 4a: Spike U1, U2, U4 (security UNKNOWNs)

Time box: half a day. Source reading plus one runtime check; findings only, no package code. U3 is in Task 3 and U5 in Task 2. U1, U2 and U3 must be closed before 1.0 sign-off.

**Files:**
- Modify: `docs/spikes.md` (U1, U2, U4)

**Interfaces:**
- Produces: U1's token mode and lifetime (used by Task 16's `session.spec.ts` and the README residual in Task 17); U2's answer (gates the v0.2 plan, design 7.5); U4's answer (may add a contract test to Task 10).

- [ ] **Step 1 (U1): Token validation mode.** Read the OpenIddict validation setup in `Umbraco.Cms.Api.Common` at `release-18.3.0` and `release-17.8.0` (`gh api repos/umbraco/Umbraco-CMS/contents/...?ref=release-18.3.0`). Record: whether access tokens are validated against the token store (reference or introspected) or self-contained, and the access-token lifetime setting and default, with file and line. **Pass:** mode and lifetime recorded with source lines. Task 16's `session.spec.ts` step (d) then confirms the runtime behaviour matches. If self-contained, Task 17 adds the README residual line.
- [ ] **Step 2 (U2): Automate authoring rights.** From the Automate docs and the `Umbraco.Automate.Core` source or package (17.x and 18.x): which permission or section a user needs to create or edit an automation, and whether an action's execution context exposes the authoring or triggering user. **Pass:** both answers recorded with sources. If authoring needs no section beyond Automate, write in `docs/spikes.md` U2: "AU-05 controls (design 7.5) are mandatory in the v0.2 plan; without them AU-05 is Critical."
- [ ] **Step 3 (U4): External-login auto-link.** Read `BackOfficeSignInManager` (and the auto-link options it uses) at both floor tags. Record whether auto-link (a) re-approves a disabled user, (b) updates the user's name from IdP claims. **Pass:** both answered with the source line. If (a) is yes: add `CoreContractTests.Auto_link_reapproval_raises_UserSaved` to Task 10's file list and record that F7 catches it. If (b) is yes: record that names are attacker-influenced from the IdP (confirms AU-04; no design change).
- [ ] **Step 4: Verify.** `docs/spikes.md` U1, U2, U4 each have a `**Result / Decision:**` with at least one source link. Expected: no "unknown" left. `docs/spikes.md` records only the authoring-rights answer and facts from public sources.
- [ ] **Step 5: M0 exit check (P4).** Run from the repo root:
  ```bash
  grep -nE '^\*\*(Gate|Result / Decision):\*\*[[:space:]]*$' docs/spikes.md
  grep -cE '^\*\*Gate:\*\* (GO|NO-GO \(fallback: .+\))' docs/spikes.md
  grep -nE '^\*\*Result / Decision:\*\*' docs/spikes.md | grep -vE 'https://|Tests?\.|CoreContractTests|HarnessTests'
  ```
  Expected: the first prints nothing (no empty line left); the second prints `5` (S0, S1, S2, S4, S6; S5 is gated in Task 5 Step 5, below); the third prints nothing (every result cites a source link or a test). Then check by eye, against the design's section 8 tables: U1 to U5 each meet their "Pass (closed) when" text; S3's decision names exactly one enumeration; every NO-GO names a fallback that a later task builds. **S5** is not an M0 item: its gate is Task 5 Step 5, and no M1 task after Task 5 starts until S5 is GO.
- [ ] **Checkpoint (Pascal commits):** `git add docs/spikes.md` / suggested message `docs: record security spikes U1, U2 and U4`.

### Milestone review A: after M0

Follow "Milestone reviews" (above the file structure). Nothing in M1 starts until this review is closed.

- [ ] **Step 1: Dispatch `qa-reviewer`** (model `sonnet`) with: the repo path; the diff `git diff <first commit>..HEAD`; `docs/design.md` section 8; `docs/spikes.md`; this plan's Tasks 1 to 4a. Question: is every spike and UNKNOWN closed by evidence that supports its gate, does each NO-GO's fallback appear in a later task, and do the CI legs, lock-file handling and PR checks work as Task 1 and Task 3 say? Expected: a JSON verdict saved to the private workspace.
- [ ] **Step 2 (Pascal): Review the spikes.** Pascal reads `docs/spikes.md` and the reviewer's verdict (README "Build approach": M0 spikes are reviewed before the build continues). Any NO-GO that changes the spec is written into `design.md` (workspace copy and `docs/design.md`) before M1.
- [ ] **Step 3: Close.** Every blocking finding fixed (TDD, in the owning task's files) and re-checked by the same reviewer once. Expected: verdict `pass` or `pass-with-advisories`.

---

## M1: backend

### Task 5: Settings, schema, migration and repository (includes S5 gate)

**Files:**
- Create: `Core/Configuration/ArchivedUsersSettings.cs`, `Core/Models/{ArchiveSource,ArchiveAuditAction,ArchivedUserRecord,ArchiveAuditEntry}.cs`, `Core/Persistence/{ArchivedUserDto,ArchiveAuditDto,IArchivedUserRepository,ArchivedUserRepository}.cs`, `Core/Migrations/{ArchivedUsersMigrationPlan,AddArchivedUserTables}.cs`, `Core/ArchivedUsersBuilderExtensions.cs`
- Create: `.github/workflows/nightly.yml` (SQL Server job only; Task 17 extends it)
- Test: `IntegrationTests/MigrationTests.cs`, `IntegrationTests/ArchivedUserRepositoryTests.cs`, `Tests/ArchivedUsersSettingsTests.cs`

**Interfaces:**
- Produces:
  - `ArchivedUsersSettings { bool HideArchivedFromUserList = true; bool HideArchivedForApiUsers = false; bool RequireReason = false; bool UnarchiveEnablesUser = false; }`, bound from section `ArchivedUsers` via `IOptionsMonitor<ArchivedUsersSettings>` with `ValidateOnStart()`. `const string SectionName = "ArchivedUsers"`.
  - `enum ArchiveSource { Manual, Bulk, CleanUp, Policy, Automate, CoreEnable, Reconcile }`, `enum ArchiveAuditAction { Archived, Unarchived, AutoDisabled, AutoArchived, Exported, AuditPurged }` (the last two are written from v0.2; declared now so the column's value set is fixed)
  - `record ArchivedUserRecord(Guid UserKey, DateTime ArchivedUtc, Guid? ArchivedByUserKey, ArchiveSource Source, string? Reason, bool WasDisabledBefore)`
  - `record ArchiveAuditEntry(Guid UserKey, ArchiveAuditAction Action, Guid? PerformedByUserKey, DateTime PerformedUtc, ArchiveSource Source, string? Reason, string? UserNameSnapshot, bool ReEnabled = false, string? Context = null)` (AU-07; `Context` max 255, never user text)
  - `IArchivedUserRepository` (requires an ambient scope; throws `InvalidOperationException` without one):
    - `Task<ArchivedUserRecord?> GetAsync(Guid userKey)`
    - `Task<IReadOnlyList<ArchivedUserRecord>> GetAllAsync()`
    - `Task<HashSet<Guid>> GetKeysAsync()`
    - `Task<bool> TryInsertAsync(ArchivedUserRecord record)` (false if a record for that key exists)
    - `Task<bool> DeleteAsync(Guid userKey)`
    - `Task InsertAuditAsync(ArchiveAuditEntry entry)`
    - `Task<IReadOnlyList<ArchiveAuditEntry>> GetAuditForUserAsync(Guid userKey)` (newest first)
  - `IUmbracoBuilder AddArchivedUsersCore(this IUmbracoBuilder builder)` registers settings and the repository (scoped). Later tasks append to it.

- [ ] **Step 1: Write the failing tests.**
  - `MigrationTests.Plan_creates_both_tables`: run the plan (by the method S0 recorded: `IPackageMigrationRunner` or `MigrationPlanExecutor`); assert `archivedUsersUser` and `archivedUsersAudit` exist with every column in spec section 7.2 (including `archivedUsersAudit.reEnabled` bit not null default 0 and `context` nvarchar(255) null, AU-07), a unique index on `archivedUsersUser.userKey`, and indexes on `archivedUsersAudit.userKey` and `performedUtc`.
  - `MigrationTests.Plan_is_safe_to_rerun`: run the step twice; no exception; tables unchanged.
  - `MigrationTests.Step_tolerates_table_created_concurrently` (AU-14b): create `archivedUsersUser` by hand first, then run the step; no exception; `archivedUsersAudit` created.
  - `ArchivedUserRepositoryTests.Audit_round_trips_ReEnabled_and_Context`.
  - `ArchivedUserRepositoryTests.Search_text_is_parameterised` (AU-12): a record whose reason is `'; DROP TABLE archivedUsersAudit;--` round-trips and both tables still exist.
  - `MigrationTests.Plan_name_and_state_are_permanent`: `new ArchivedUsersMigrationPlan().PackageName == "Umbraco.Community.ArchivedUsers"` and the final state is `"archived-users-1"`.
  - `ArchivedUserRepositoryTests.Insert_then_get_round_trips_with_utc_kind`: `ArchivedUtc.Kind == DateTimeKind.Utc` and all fields equal.
  - `ArchivedUserRepositoryTests.TryInsert_duplicate_key_returns_false_and_keeps_one_row` (Review Focus 1).
  - `ArchivedUserRepositoryTests.Enums_are_stored_as_strings`: raw `SELECT source FROM archivedUsersUser` returns `"CleanUp"`.
  - `ArchivedUserRepositoryTests.Delete_record_keeps_audit_rows`.
  - `ArchivedUserRepositoryTests.Repository_without_scope_throws`.
  - `ArchivedUsersSettingsTests.Defaults_match_spec`: `HideArchivedFromUserList true`, `HideArchivedForApiUsers false`, `RequireReason false`, `UnarchiveEnablesUser false`.

- [ ] **Step 2: Run.** `dotnet test src/Umbraco.Community.ArchivedUsers.slnx --filter "FullyQualifiedName~MigrationTests|FullyQualifiedName~ArchivedUserRepositoryTests|FullyQualifiedName~ArchivedUsersSettingsTests"`. Expected: build fails, types not found.

- [ ] **Step 3: Implement.** NPoco DTOs with `[TableName]`, `[PrimaryKey("id", AutoIncrement = true)]`, `[Index]`, `[Length(32)]`, `[Length(500)]`, `[Length(255)]`, `[NullSetting(NullSetting = NullSettings.Null)]` per the spec tables; enums mapped to strings in the repository. `AddArchivedUserTables : AsyncMigrationBase` creates each table only `if (!TableExists(...))`; if a create throws, re-check `TableExists` and continue when it now exists, else rethrow (why-comment: another node may run the same unattended migration, AU-14b). `ArchivedUsersMigrationPlan : PackageMigrationPlan` with `DefinePlan() => To<AddArchivedUserTables>("archived-users-1")`. Repository reads `IScopeAccessor.AmbientScope.Database` and builds every query with `Sql<ISqlContext>` and parameters, never string concatenation (AU-12). `TryInsertAsync` catches the insert exception and returns false if `GetAsync` then finds a record, otherwise rethrows (DB-agnostic duplicate detection). Why-comment: no FK to `umbracoUser` on purpose (spec 7.2, issue #23987).

- [ ] **Step 4: Run.** Same command. Expected: "Failed: 0".

- [ ] **Step 5: The S5 gate (SQLite and SQL Server).** S5 is the one spike gated here rather than in M0, because it needs the real migration. SQLite: Step 4 already ran it. SQL Server: the reliable run is CI on Linux; locally on Apple Silicon `mcr.microsoft.com/mssql/server:2022-latest` needs `--platform linux/amd64` emulation and may be slow or crash, so a local run is optional.
  - Create `.github/workflows/nightly.yml` (triggers: daily cron, `workflow_dispatch`, and `pull_request` with `paths: ['src/Umbraco.Community.ArchivedUsers/Core/**', 'src/Umbraco.Community.ArchivedUsers.IntegrationTests/**', '.github/workflows/nightly.yml']`, because `workflow_dispatch` only works once the file is on `main`; actions SHA-pinned) with job `integration-sqlserver`: a `mcr.microsoft.com/mssql/server:2022-latest` service container, floor-leg locked restore, then `dotnet test src/Umbraco.Community.ArchivedUsers.IntegrationTests -c Release` with the SQL Server settings S0 recorded (`Tests:Database:DatabaseType=SqlServer` and the master connection string as environment variables; under the S0 fallback, the fixture's connection string). Task 17 adds the Playwright job.
  - Pascal runs this task's checkpoint commit early and pushes `feat/v0.1`; the open draft PR from Task 1 runs the job. Then `gh run watch`. Expected: "Failed: 0".
  - Demo site on SQL Server (in the same CI job, or locally with emulation): point it at a fresh database, start it, restart it; both tables exist and `umbracoKeyValue` has `Umbraco.Core.Upgrader.State+Umbraco.Community.ArchivedUsers` = `archived-users-1`.
  - Record `**Gate:** GO` in `docs/spikes.md` S5 with the run URL. NO-GO: `**Gate:** NO-GO (fallback: per-provider branch in AddArchivedUserTables)`, build that branch, and re-run before Task 6.

- [ ] **Checkpoint (Pascal commits):** `git add src/ docs/spikes.md .github/workflows/nightly.yml` / suggested message `feat: add archived user tables, migration and repository`.

### Task 6: User visibility check

**Files:**
- Create: `Core/Security/UserVisibility.cs`
- Test: `Tests/UserVisibilityTests.cs`

**Interfaces:**
- Produces: `static class UserVisibility { bool CanSee(IUser performer, IUser target); bool IsAdmin(IUser user); bool IsSuperUser(Guid key); }`. Pure functions, no state, so a static is fine under P1/P2. `IsAdmin` is used by AU-16's status masking.

- [ ] **Step 1: Write the failing tests** (NSubstitute `IUser` with `Key` and `Groups`):
  - `Superuser_target_hidden_from_everyone_but_superuser`: target key `Constants.Security.SuperUserKey`; admin performer → false; superuser performer → true.
  - `Admin_target_hidden_from_non_admin`: target in group alias `Constants.Security.AdminGroupAlias`; user-manager performer → false.
  - `Admin_target_visible_to_admin` → true.
  - `Non_admin_target_visible_to_non_admin` → true.
  - `Performer_can_see_self` → true.
  - `IsAdmin_true_only_for_admin_group_member`.
  - `IsSuperUser_true_only_for_SuperUserKey`.
- [ ] **Step 2: Run** `dotnet test src/Umbraco.Community.ArchivedUsers.Tests --filter UserVisibilityTests`. Expected: FAIL, `UserVisibility` not found.
- [ ] **Step 3: Implement** the rules. Why-comment: mirrors `UserService.CreateBaseUserFilter` (about line 1487 on `main` at `f7f06d9`); re-check on each minor; `CoreContractTests.FilterAsync_hides_admins_from_non_admin` is the tripwire. Second why-comment: this is **not** a substitute for core's `AuthorizeAccessAsync`, which Task 7 also calls; it adds the superuser rule core's policy lacks (AU-01).
- [ ] **Step 4: Run.** Expected: "Passed: 7".
- [ ] **Checkpoint (Pascal commits):** `git add src/` / suggested message `feat: add user visibility check mirroring core rules`.

### Task 7: Archive service, single and bulk (F1, F2, F8)

**Files:**
- Create: `Core/Models/{ArchiveResultStatus,UserArchiveResult}.cs`, `Core/Notifications/{UserArchiveEvent,UserArchivingNotification,UserArchivedNotification}.cs`, `Core/Services/{IArchivedUserService,ArchivedUserService,ArchiveUserLocks}.cs`
- Modify: `Core/ArchivedUsersBuilderExtensions.cs` (register the service scoped, `ArchiveUserLocks` as a singleton, plus `TimeProvider.System` if not registered)
- Test: `Tests/ArchivedUserServiceArchiveTests.cs`, `Tests/ArchiveUserLocksTests.cs`, `IntegrationTests/ArchiveFlowTests.cs`

**Interfaces:**
- Consumes: `IArchivedUserRepository`, `UserVisibility`, the S4 notes (status map, `IUserPermissionService.AuthorizeAccessAsync` call, stamp rotation route, core audit call), `IBackOfficeUserManager`, `IAuditService`.
- Produces:
  - `enum ArchiveResultStatus { Archived, Unarchived, Self, Invited, AlreadyArchived, NotArchived, NotPermitted, NotFound, Cancelled, CoreRefused }`
  - `record UserArchiveResult(Guid Id, ArchiveResultStatus Status, UserOperationStatus? CoreStatus = null, string? Message = null)`
  - `record UserArchiveEvent(Guid UserKey, Guid? PerformerKey, ArchiveSource Source, string? Reason)`
  - `UserArchivingNotification : CancelableObjectNotification<UserArchiveEvent>`, `UserArchivedNotification : ObjectNotification<UserArchiveEvent>`, both `(UserArchiveEvent target, EventMessages messages)`
  - `IArchivedUserService.ArchiveAsync(Guid performerKey, ISet<Guid> userKeys, string? reason, ArchiveSource source) : Task<IReadOnlyList<UserArchiveResult>>` (reason is already normalised by the caller)
  - `sealed class ArchiveUserLocks { Task<IDisposable> AcquireAsync(Guid userKey, CancellationToken cancellationToken = default); }` (DI singleton; 64 `SemaphoreSlim(1,1)` stripes, stripe = `(userKey.GetHashCode() & int.MaxValue) % 64`; disposing the handle releases). Task 9's unarchive uses it too (P6).

- [ ] **Step 1: Write the failing unit tests** (substitutes for `IUserService`, `IUserPermissionService`, `IBackOfficeUserManager`, `IAuditService`, `IArchivedUserRepository`, `ICoreScopeProvider`, `IEventAggregator`, `IEventMessagesFactory`, a fake `TimeProvider`, a `FakeLogger`, and a recording fake of `ArchiveUserLocks`):
  - `Archive_enabled_user_disables_then_writes_record_and_one_audit_row`: `DisableAsync(performer, Arg.Is<ISet<Guid>>(s => s.Single() == target))` received once; `TryInsertAsync` with `WasDisabledBefore == false`, `Source == Manual`, `ArchivedByUserKey == performer`, `ArchivedUtc == fake now`; `InsertAuditAsync` once with `Action == Archived` and `UserNameSnapshot == target.Name`; result `Archived`.
  - `Archive_disabled_user_does_not_call_core_and_sets_WasDisabledBefore`.
  - `Archive_self_returns_Self_and_writes_nothing`.
  - `Archive_invited_user_returns_Invited_and_writes_nothing`.
  - `Archive_already_archived_returns_AlreadyArchived` (record exists before, user disabled).
  - `Archive_stale_record_reconciles_then_archives` (AU-03): record exists, user enabled → `DeleteAsync` then an `Unarchived` audit row with `Source == Reconcile` and null performer, then `DisableAsync`, a new record, and an `Archived` row; result `Archived`.
  - `Archive_unknown_key_returns_NotFound_to_admin`.
  - `Archive_unknown_key_returns_NotPermitted_to_non_admin` (AU-16, T13).
  - `Archive_invisible_target_returns_NotPermitted_and_never_calls_core` (admin target, user-manager performer).
  - `Archive_refused_by_core_permission_rule_returns_NotPermitted_and_never_calls_DisableAsync` (AU-01): `AuthorizeAccessAsync` returns a failure while `CanSee` would pass; `DisableAsync` not received; nothing written.
  - `Archive_superuser_target_by_admin_returns_NotPermitted` (AU-01, T1): admin performer, `SuperUserKey` target; core not called. (A superuser archiving itself is `Self`.)
  - `Archive_by_disabled_or_missing_performer_returns_NotPermitted_for_every_key` (P9; `[Theory]`, two cases): performer `UserState == Disabled`, then performer not found; keys {editor, admin} → both `NotPermitted`; `DisableAsync`, `TryInsertAsync` and `AuthorizeAccessAsync` not received.
  - `Archive_rotates_security_stamp_after_disable` (AU-02, T4c): `UpdateSecurityStampAsync` received once, after `DisableAsync`, for the target.
  - `Archive_already_disabled_user_does_not_rotate_stamp`.
  - `Stamp_rotation_failure_logs_warning_and_still_archives` (AU-02): rotation throws → result `Archived`, record and audit written, one Warning logged.
  - `Archive_writes_one_core_audit_entry` (AU-07): `IAuditService` write received once with the S4 call, comment containing action and source and **not** the reason or name.
  - `Logs_never_contain_reason_or_name` (AU-09, AU-12): run a cancel, a core refusal and a scope failure with reason `"secret-reason"` and name `"Secret Name"`; no captured log message or rendered template contains either string; the user key appears.
  - `Core_refusal_returns_CoreRefused_with_status_and_writes_nothing`.
  - `Cancelled_notification_returns_Cancelled_with_message_and_never_calls_core`: a handler calls `CancelOperation(new EventMessage("Policy", "Locked by HR", EventMessageType.Error))`; result `Message == "Locked by HR"`.
  - `Insert_collision_after_disable_returns_AlreadyArchived_and_logs_warning` (Review Focus 1).
  - `Scope_failure_after_disable_logs_one_Error_and_returns_CoreRefused_without_CoreStatus` (P10): `TryInsertAsync` throws after a successful `DisableAsync` → result `CoreRefused` with `CoreStatus == null`; exactly one Error logged, carrying the user key; no `UserArchivedNotification`.
  - `Scope_failure_then_retry_archives_the_now_disabled_user` (P10): the same user, second call, repository now healthy → result `Archived`, `DisableAsync` not received again, record written with `WasDisabledBefore == true`. Why-comment in the test: this is the accepted gap's consequence (design 7.2); the user stays disabled on unarchive, which fails safe.
  - `Archive_holds_the_target_lock_from_state_read_to_record_write` (P6): the recording lock fake shows `AcquireAsync(target)` before the user read and `GetAsync(record)`, and the handle disposed after `InsertAuditAsync`; one acquire per key in a bulk call, never two held at once.
  - `Bulk_returns_one_result_per_key_and_continues_after_a_skip`: keys {self, invited, ok1, ok2} → statuses in that order of evaluation: `Self`, `Invited`, `Archived`, `Archived`.
  - `Archived_notification_published_after_scope_complete`: `Received.InOrder` scope `Complete()` then `PublishAsync(UserArchivedNotification)`.
  - `ArchiveUserLocksTests`: `Same_key_waits_until_the_first_handle_is_disposed`; `Cancellation_while_waiting_throws_OperationCanceledException`; `Disposing_twice_releases_once`.
- [ ] **Step 2: Run** `dotnet test src/Umbraco.Community.ArchivedUsers.Tests --filter "FullyQualifiedName~ArchivedUserServiceArchiveTests|FullyQualifiedName~ArchiveUserLocksTests"`. Expected: FAIL, types not found.
- [ ] **Step 3: Implement `ArchiveUserLocks`, then `ArchiveAsync`** with the spec's per-user flow (design 7.2, "Archive flow per user", steps 0 to 8):
  - Load the performer and targets with `IUserService.GetAsync`. Performer missing or `Disabled` → every key `NotPermitted`, nothing else runs (P9).
  - For each key in order, acquire `ArchiveUserLocks.AcquireAsync(key)` and hold it until the record and audit are written or the key is skipped (P6). Why-comment: the lock closes the same-node race that could store a wrong `WasDisabledBefore`; it is not re-entrant, so `UserArchivingNotification` handlers must not call the service for the same user.
  - Status checks, in this order: NotFound (`NotPermitted` when `!UserVisibility.IsAdmin(performer)`, AU-16); Self; NotPermitted (superuser target, or `AuthorizeAccessAsync(performer, { key })` fails, or `!CanSee`, AU-01; why-comment "`IUserService` does not authorise; see design 4 and AU-01"); AlreadyArchived (record and user disabled); stale record reconcile (record and user enabled, AU-03) in a private method that Task 10's `ReconcileAsync` also calls; Invited.
  - Publish the cancelable `UserArchivingNotification` with `IEventAggregator.PublishCancelableAsync`; on cancel return `Cancelled` with the first event message.
  - Call `DisableAsync` one user at a time, only if `UserState != Disabled` (why-comment: core returns one status per call). On core failure return `CoreRefused` with the status.
  - After a successful disable, rotate the stamp by the S4 route; on failure log a Warning and carry on (why-comment: AU-02, and why a failure is non-fatal).
  - In one `ICoreScope`: `TryInsertAsync` the record and `InsertAuditAsync` the row, then `Complete()`. A `false` from `TryInsertAsync` → `AlreadyArchived` plus a Warning. If the scope throws after a successful disable, log one Error ("disabled but not archived", user key only) and return `CoreRefused` with no core status (P10).
  - After the scope: write the core audit entry; publish `UserArchivedNotification`; release the lock.
- [ ] **Step 4: Run.** Same command as Step 2. Expected: "Failed: 0" and "Passed: 29" (26 service results, the `[Theory]` counting twice, plus 3 lock tests).
- [ ] **Step 5: Integration tests** in `ArchiveFlowTests` (real services):
  - `Archive_disables_user_and_persists_record_and_audit`: a logged-in editor; after `ArchiveAsync`, `IUserService.GetAsync(key).UserState == Disabled`, `GetAsync(key)` record exists, `GetAuditForUserAsync(key).Count == 1`.
  - `Archiving_and_archived_notifications_fire_once_each`.
  - `Archive_invited_user_returns_Invited_and_writes_nothing` (P9).
  - `Non_admin_cannot_archive_admin_and_admin_stays_enabled` (T1, AU-01): user-manager performer, admin target → `NotPermitted`; the admin's `IsApproved` is still true.
  - `Admin_cannot_archive_superuser` (T1) → `NotPermitted`; superuser still enabled.
  - `Archive_changes_security_stamp` (T4c, AU-02).
  - `Archived_admin_cannot_then_archive_the_other_admin` (P9): admins A and B; `ArchiveAsync(A, {B})` → `Archived`; then `ArchiveAsync(B, {A})` → `NotPermitted`; A is still enabled and has no record.
  - `Concurrent_double_archive_leaves_one_record_one_audit_row_no_exception` (P6, T16, Review Focus 1), `[Category("Concurrency")]`: an enabled editor; two `IArchivedUserService` instances from two separate DI scopes, started together with `Task.WhenAll`; results are one `Archived` and one `AlreadyArchived`; no exception; one record with `WasDisabledBefore == false`; one `Archived` audit row; user `Disabled`. No `Assume`, `Ignore` or provider check in this test: it runs on SQLite in every build and on SQL Server nightly.
  Run `dotnet test src/Umbraco.Community.ArchivedUsers.IntegrationTests --filter ArchiveFlowTests`. Expected: "Failed: 0", "Skipped: 0".
- [ ] **Step 6: Nightly concurrency check.** In `nightly.yml`'s `integration-sqlserver` job, add a step `dotnet test ... --filter TestCategory=Concurrency --logger "trx;LogFileName=concurrency.trx"` followed by a check that the TRX shows `executed="1"` (or more) and `notExecuted="0"`, so a silent skip on SQL Server fails the job. Expected on the PR run: green.
- [ ] **Checkpoint (Pascal commits):** `git add src/ .github/workflows/nightly.yml` / suggested message `feat: archive users through IUserService with audit and notifications`.

### Task 8: Clean-up preview and archive all disabled (F3)

**Files:**
- Create: `Core/Models/CleanUpPreview.cs`
- Modify: `Core/Services/{IArchivedUserService,ArchivedUserService}.cs`
- Test: `Tests/ArchivedUserServiceCleanUpTests.cs`, `IntegrationTests/CleanUpTests.cs`

**Interfaces:**
- Consumes: `ArchiveAsync` (Task 7); `IArchivedUserRepository.GetKeysAsync()` (Task 5) for the archived set, read inside an `ICoreScope` (P12a: `GetArchivedKeysAsync` doesn't exist until Task 9).
- Produces:
  - `record UserNameItem(Guid Id, string Name)`; `record CleanUpPreview(int Count, IReadOnlyList<UserNameItem> Sample)` (sample max 10, ordered by name)
  - `Task<CleanUpPreview> PreviewCleanUpAsync(Guid performerKey)`
  - `Task<IReadOnlyList<UserArchiveResult>> ArchiveAllDisabledAsync(Guid performerKey, string? reason)`

- [ ] **Step 1: Write the failing tests.**
  - `Preview_counts_disabled_non_archived_visible_users_and_samples_ten_by_name`: 12 disabled, 2 already archived → `Count == 10`, `Sample.Count == 10`, names ascending.
  - `Preview_with_none_returns_zero_and_empty_sample`.
  - `ArchiveAllDisabled_archives_each_with_source_CleanUp` (calls `ArchiveAsync` path; every record `Source == CleanUp`).
  - `ArchiveAllDisabled_with_none_writes_nothing`.
  - `ArchiveAllDisabled_logs_warning_above_1000`.
  - Integration `CleanUpTests.Non_admin_preview_excludes_disabled_admins`.
  - Integration `CleanUpTests.Non_admin_clean_up_leaves_disabled_admin_unarchived` (T1, AU-01): after `ArchiveAllDisabledAsync(userManager)`, the disabled admin has no record.
  - `Preview_sample_never_includes_superuser`.
  - Integration `CleanUpTests.Preview_counts_server_side_when_HideDisabledUsersInBackOffice_is_on` (set the core option in `CustomTestSetup`).
- [ ] **Step 2: Run** `dotnet test src/Umbraco.Community.ArchivedUsers.slnx --filter "FullyQualifiedName~CleanUp"`. Expected: FAIL.
- [ ] **Step 3: Implement** the enumeration chosen in `docs/spikes.md` S3: `FilterAsync(performer, new UserFilter { IncludeUserStates = { UserState.Disabled } }, 0, int.MaxValue)` by default, or paged `IUserService` reads plus `UserVisibility.CanSee` if S3 showed the core setting empties it. Exclude the keys from `IArchivedUserRepository.GetKeysAsync()`. Why-comment cites S3.
- [ ] **Step 4: Run.** Expected: "Failed: 0".
- [ ] **Checkpoint (Pascal commits):** `git add src/` / suggested message `feat: preview and archive all disabled users`.

### Task 9: Unarchive and archived reads (F4 data, F5)

**Files:**
- Create: `Core/Notifications/{UserUnarchiveEvent,UserUnarchivedNotification}.cs`, `Core/Models/{ArchivedUserView,ArchivedUserOrder}.cs`
- Modify: `Core/Services/{IArchivedUserService,ArchivedUserService}.cs`
- Test: `Tests/ArchivedUserServiceUnarchiveTests.cs`, `Tests/ArchivedUserServiceQueryTests.cs`

**Interfaces:**
- Produces:
  - `record UserUnarchiveEvent(Guid UserKey, Guid? PerformerKey, ArchiveSource Source, bool ReEnabled)`; `UserUnarchivedNotification : ObjectNotification<UserUnarchiveEvent>`
  - `Task<IReadOnlyList<UserArchiveResult>> UnarchiveAsync(Guid performerKey, ISet<Guid> userKeys, bool? enable, ArchiveSource source)`. Throws nothing for `enable: true` with the setting off; the controller rejects that with 400 before calling (Task 11). The service still never enables when the setting is off or `WasDisabledBefore` is true (defence in depth, AU-11).
  - `enum ArchivedUserOrder { ArchivedUtc, Name }`; `record ArchivedUserView(IUser User, ArchivedUserRecord Record, string? ArchivedByName, bool IsStale)` (`IsStale` = record exists and `User.UserState != Disabled`, AU-03)
  - `Task<PagedModel<ArchivedUserView>> GetArchivedAsync(Guid performerKey, int skip, int take, string? filter, ArchivedUserOrder orderBy, Direction direction)`
  - `Task<ArchivedUserView?> GetArchivedUserAsync(Guid performerKey, Guid userKey)` (null if not archived or not visible)
  - `Task<HashSet<Guid>> GetArchivedKeysAsync()`; `Task<bool> IsArchivedAsync(Guid userKey)`

- [ ] **Step 1: Write the failing unarchive tests.**
  - `Unarchive_by_disabled_or_missing_performer_returns_NotPermitted_for_every_key` (P9; same two cases as Task 7).
  - `Unarchive_holds_the_target_lock_around_delete_enable_and_audit` (P6; same recording lock fake as Task 7).
  - `Unarchive_removes_record_writes_Unarchived_audit_and_leaves_user_disabled` (D3, `enable: null`, setting false): `EnableAsync` not received; result `Unarchived`; audit `ReEnabled == false`.
  - `Unarchive_deletes_record_then_enables_then_writes_audit_in_one_scope`: `Received.InOrder` `CreateCoreScope`, `DeleteAsync`, `EnableAsync`, `InsertAuditAsync`, `Complete` (why: the F7 handler's notification is deferred to our scope's completion and then finds no record, so no duplicate `CoreEnable` row; design 7.2).
  - `Unarchive_with_setting_on_and_WasDisabledBefore_false_enables` and audit `ReEnabled == true` (AU-07).
  - `Unarchive_with_setting_on_and_WasDisabledBefore_true_stays_disabled`.
  - `Unarchive_with_enable_true_and_WasDisabledBefore_true_stays_disabled` (AU-11, T10).
  - `Unarchive_with_enable_true_and_setting_off_does_not_enable` (AU-11, T10; service-level defence in depth).
  - `Unarchive_with_enable_false_and_setting_on_stays_disabled` (AU-11).
  - `Unarchive_not_archived_returns_NotArchived`; `Unarchive_invisible_returns_NotPermitted`; `Unarchive_unknown_returns_NotFound_to_admin_and_NotPermitted_to_non_admin` (AU-16).
  - `Unarchive_refused_by_core_permission_rule_returns_NotPermitted_and_never_enables` (AU-01): `AuthorizeAccessAsync` fails, setting on → `EnableAsync` not received, record kept.
  - `Unarchive_writes_one_core_audit_entry` (AU-07).
  - `Enable_refused_by_core_still_unarchives_with_CoreStatus_and_ReEnabled_false` (audit row `ReEnabled == false`).
  - `UserUnarchivedNotification_carries_ReEnabled`.
- [ ] **Step 2: Write the failing query tests.**
  - `GetArchived_default_order_is_ArchivedUtc_descending`.
  - `GetArchived_sorts_by_name_ascending`.
  - `GetArchived_filter_matches_name_or_email_case_insensitively`.
  - `GetArchived_pages_in_memory_with_total_of_filtered_set`.
  - `GetArchived_take_zero_returns_no_items_and_the_filtered_total` (P14).
  - `GetArchived_hides_admins_from_non_admin`.
  - `GetArchived_skips_record_whose_user_no_longer_exists` (Review Focus 3).
  - `GetArchived_shows_current_name_after_rename` (Review Focus 3).
  - `GetArchived_returns_null_ArchivedByName_when_performer_deleted` (Review Focus 5).
  - `GetArchived_logs_warning_above_5000_records`.
  - `GetArchivedUser_returns_null_when_not_visible`.
  - `GetArchived_flags_record_of_enabled_user_as_stale_and_writes_nothing` (AU-03, T3): `IsStale == true`; `DeleteAsync` and `InsertAuditAsync` not received (no state change on read, AU-14).
- [ ] **Step 3: Run** `dotnet test src/Umbraco.Community.ArchivedUsers.Tests --filter "FullyQualifiedName~Unarchive|FullyQualifiedName~QueryTests"`. Expected: FAIL.
- [ ] **Step 4: Implement.** Unarchive flow per spec 7.2: the same performer check and per-user `ArchiveUserLocks` lock as archive (P6, P9); the same authorisation as archive (AU-01, AU-16); effective enable = `(enable ?? settings.UnarchiveEnablesUser) && settings.UnarchiveEnablesUser && !record.WasDisabledBefore` (AU-11, why-comment cites it); one outer scope deletes the record, calls `EnableAsync` if enabling, inserts the `Unarchived` audit row with `ReEnabled` = the enable's actual success, and completes; then the core audit entry; then publish. Query per spec 7.2 "Archived view query": read all records, load users by key with `IUserService.GetAsync(keys)`, apply `CanSee`, then search, sort and page in memory. Load performer names in one `GetAsync(keys)` call.
  - Only if `docs/spikes.md` S2 records the deferral gating test as NO-GO (P3): add `Core/Services/UnarchiveInProgress.cs`, a DI-scoped `sealed class UnarchiveInProgress { void Add(Guid userKey); void Remove(Guid userKey); bool Contains(Guid userKey); }`; `UnarchiveAsync` adds the key before `EnableAsync` and removes it in a `finally`; Task 10's saved handler skips contained keys. Add the unit test `Unarchive_marks_key_in_progress_during_EnableAsync` (the marker contains the key when `EnableAsync` is called and not afterwards). Why-comment cites S2 and design 7.2.
- [ ] **Step 5: Run.** Expected: "Failed: 0".
- [ ] **Checkpoint (Pascal commits):** `git add src/` / suggested message `feat: unarchive users and query the archived set`.

### Task 10: Keep the flag honest (F7)

**Files:**
- Create: `Core/Handlers/{UserSavedArchiveSyncHandler,UserDeletedArchiveCleanupHandler,ReconcileOnStartupHandler}.cs`
- Modify: `Core/ArchivedUsersBuilderExtensions.cs` (`AddNotificationAsyncHandler<UserSavedNotification, UserSavedArchiveSyncHandler>()`, same for deleted, and `AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, ReconcileOnStartupHandler>()`), `Core/Services/{IArchivedUserService,ArchivedUserService}.cs` (`ReconcileAsync`)
- Test: `IntegrationTests/FlagSyncTests.cs`, `IntegrationTests/ReconcileTests.cs`

**Interfaces:**
- Consumes: `IArchivedUserRepository`, `UserUnarchivedNotification`, `IBackOfficeSecurityAccessor`, Task 7's private stale-record reconcile.
- Produces: `Task<int> IArchivedUserService.ReconcileAsync(CancellationToken cancellationToken)` (records removed; reused by the v0.2 job, F12). `IArchivedUserRepository.DeleteAsync` returning `true` only for the call that removed the row is the cluster de-duplication rule.

- [ ] **Step 1: Write the failing tests.**
  - `Core_enable_of_archived_user_by_logged_in_admin_writes_CoreEnable_with_that_admin_as_performer` (T11, AU-07): set the backoffice security accessor's current user to an admin, enable via `IUserService.EnableAsync`; audit `Source == CoreEnable`, `PerformedByUserKey == admin.Key`.
  - `Core_enable_without_backoffice_user_writes_CoreEnable_with_null_performer`.
  - `Package_unarchive_with_enable_writes_exactly_one_Unarchived_row` (no `CoreEnable` duplicate).
  - `Saving_an_archived_user_that_stays_disabled_changes_nothing` (e.g. rename).
  - `Deleting_a_never_logged_in_archived_user_removes_record_and_keeps_audit`.
  - `Core_enable_publishes_UserUnarchivedNotification_with_source_CoreEnable`.
  - `ReconcileTests.Record_of_enabled_user_is_removed_with_Reconcile_audit` (T3, AU-03): insert a record directly for an enabled user; `ReconcileAsync` returns 1; record gone; one `Unarchived` row with `Source == Reconcile`, null performer.
  - `ReconcileTests.Record_of_deleted_user_is_removed_with_Reconcile_audit`.
  - `ReconcileTests.Record_of_disabled_user_is_kept`.
  - `ReconcileTests.Second_concurrent_reconcile_writes_no_second_row` (AU-14b): two `ReconcileAsync` calls on parallel scopes for the same stale record → exactly one `Reconcile` row.
  - `ReconcileTests.Startup_handler_reconciles_and_swallows_exceptions`: with the repository throwing, the handler logs a Warning and does not throw.
  - If Task 4a U4 found that auto-link re-approves a disabled user: `CoreContractTests.Auto_link_reapproval_raises_UserSaved`.
- [ ] **Step 2: Run** `dotnet test src/Umbraco.Community.ArchivedUsers.IntegrationTests --filter "FullyQualifiedName~FlagSyncTests|FullyQualifiedName~ReconcileTests"`. Expected: FAIL.
- [ ] **Step 3: Implement.** Saved handler: for each saved user with `UserState != Disabled` that has a record, in its own scope delete the record and, only if the delete removed it, insert `Unarchived` / `CoreEnable` / performer = `IBackOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key` (null when absent) / name snapshot; publish `UserUnarchivedNotification` with `ReEnabled = true`. If Task 9 built `UnarchiveInProgress` (S2 deferral NO-GO), skip keys it contains. Deleted handler: delete the record only. `ReconcileAsync`: read all records, load their users by key, and for each record whose user is missing or not `Disabled`, run Task 7's stale-record reconcile (delete; audit only if the delete removed the row). `ReconcileOnStartupHandler`: only when `IRuntimeState.Level == RuntimeLevel.Run`; calls `ReconcileAsync`; catches every exception and logs a Warning with the count or the exception type (why-comment: startup must never fail because of housekeeping; GET stays read-only, AU-14; design F7a).
  - If `docs/spikes.md` S2 is NO-GO: instead add `CoreEnableActionFilter : IAsyncActionFilter` matching core's enable-user controller type, running the same logic after a successful result, and note in the README that SQL or uSync enables are caught by startup reconciliation (F7a) and the v0.2 schedule (F12).
- [ ] **Step 4: Run.** Expected: "Failed: 0".
- [ ] **Checkpoint (Pascal commits):** `git add src/` / suggested message `feat: clear the archive flag when core enables or deletes a user, and reconcile at startup`.

### Task 11: Management API controllers and security tests

**Files:**
- Create: `Web/Controllers/{ArchivedUsersControllerBase,ArchivedUsersQueryController,ArchivedUsersCommandController,ReasonValidator}.cs`, `Web/Controllers/Models/*.cs`, `Web/OpenApi/ArchivedUsersOpenApi.cs`, `Web/ArchivedUsersComposer.cs`
- Test: `Tests/ReasonValidatorTests.cs`, `Tests/ControllerConventionTests.cs`, `IntegrationTests/Api/ArchivedUsersApiTests.cs`, `IntegrationTests/Api/ArchivedUsersSecurityTests.cs`

**Interfaces:**
- Consumes: `IArchivedUserService` (Tasks 7 to 9), `IUserPresentationFactory` (sync or async per S1 notes).
- Produces the v1 contract (routes relative to `/umbraco/management/api/v1/`):

| Method | Route | Request | Response |
|---|---|---|---|
| GET | `archived-users` | `skip=0` (≥ 0), `take=100` (0 to 1,000), `filter`, `orderBy` (`archivedUtc`\|`name`), `orderDirection`; out of range or unknown `orderBy` → 400 (P14) | `PagedViewModel<ArchivedUserResponseModel>` |
| GET | `archived-users/{id:guid}` | | `ArchivedUserResponseModel`, 404 if not archived or not visible |
| POST | `archived-users/archive` | `ArchiveUsersRequestModel { Guid[] UserIds; string? Reason }` | `UserArchiveResultResponseModel[]` |
| POST | `archived-users/unarchive` | `UnarchiveUsersRequestModel { Guid[] UserIds; bool? Enable }` | `UserArchiveResultResponseModel[]` |
| GET | `archived-users/clean-up/preview` | | `CleanUpPreviewResponseModel { int Count; UserNameResponseModel[] Sample }` |
| POST | `archived-users/clean-up` | `CleanUpRequestModel { string? Reason }` | `UserArchiveResultResponseModel[]` |
| GET | `archived-users/config` | | `ArchivedUsersConfigResponseModel { bool HideArchivedFromUserList; bool RequireReason; bool UnarchiveEnablesUser }` |

  - `ArchivedUserResponseModel { UserResponseModel User; DateTime ArchivedUtc; ArchivedByResponseModel? ArchivedBy; string? Reason; string Source; bool Stale }`, `ArchivedByResponseModel { Guid Id; string? Name }` (null `ArchivedBy` = system; `Stale` from `ArchivedUserView.IsStale`, AU-03).
  - `UserArchiveResultResponseModel { Guid Id; string Status; string? Message }`. `Status` is the enum name, or `CoreRefused:<UserOperationStatus>`.
  - Request models contain exactly the fields above and nothing else (AU-12). Every POST action takes `[FromBody]` and the controller carries `[Consumes("application/json")]` (AU-14).
  - `enable: true` while `UnarchiveEnablesUser` is `false` → 400 `ProblemDetails` with title "Unarchive cannot enable users on this site" (AU-11).
  - Source rule: archive and unarchive with one id → `Manual`, more → `Bulk`. Clean-up → `CleanUp`.
  - `static class ReasonValidator { Attempt<string?, ReasonError> Normalise(string? raw, bool required); }`, `enum ReasonError { Required, TooLong }`; max 500 after trim.

- [ ] **Step 1: Write the failing unit tests.**
  - `ReasonValidatorTests`: `null` → success null; `"  x  "` → `"x"`; `"   "` with required → `Required`; `"   "` not required → success null; 500 chars → success; 501 chars → `TooLong`; `"<img src=x onerror=alert(1)>"` → returned unchanged (escaping is the renderer's job); `"Left 24 Oct 🎉 Zoë 李"` → unchanged (Review Focus 2).
  - `ControllerConventionTests.Every_controller_is_a_secured_management_api_controller`: reflect over the RCL assembly; each `Controller`-named type is assignable to `ManagementApiControllerBase`, resolves an `AuthorizeAttribute` with `Policy == AuthorizationPolicies.SectionAccessUsers` (inherit true), has `ApiExplorerSettings.GroupName == "Archived Users"`, and no method or class has `AllowAnonymousAttribute`.
- [ ] **Step 2: Write the failing API and security tests** (Umbraco test server per S0, or the S0 fallback):
  - Security: `Unauthenticated_gets_401_on_every_route` (all 7, T14); `User_without_Users_section_gets_403_on_every_route` (T14); `Non_admin_get_admin_by_id_returns_404`; `Non_admin_list_excludes_archived_admins`; `Non_admin_preview_excludes_admins`; `Result_for_unseen_user_carries_no_name_or_email`.
  - Write-route authorisation, T1 (AU-01), each asserting the per-user status **and** reading the target back to prove `IsApproved` is unchanged:
    - `Non_admin_archive_admin_returns_NotPermitted_and_admin_stays_enabled`.
    - `Non_admin_unarchive_archived_admin_returns_NotPermitted_and_record_kept`.
    - `Non_admin_unarchive_archived_admin_with_enable_true_returns_NotPermitted_and_admin_stays_disabled` (setting on).
    - `Non_admin_clean_up_does_not_archive_disabled_admin`.
    - `Admin_archive_superuser_returns_NotPermitted_and_superuser_stays_enabled`.
    - `Admin_unarchive_with_enable_on_superuser_record_returns_NotPermitted` (record inserted directly).
  - Status masking, T13 (AU-16): `Non_admin_gets_NotPermitted_for_unknown_key_and_for_admin_key_alike`; `Admin_gets_NotFound_for_unknown_key`.
  - Mass assignment and content type, T12 (AU-12, AU-14): `Body_fields_source_performerKey_archivedUtc_are_ignored` (post them with junk values; the record has `Source == Manual`, performer = caller, `ArchivedUtc` = server time); `Form_encoded_post_returns_415` on all three POST routes.
  - Unarchive policy, T10 (AU-11): `Enable_true_with_setting_off_returns_400_and_user_stays_disabled`; `Enable_true_with_setting_on_and_WasDisabledBefore_true_stays_disabled_and_audit_ReEnabled_false`; `Enable_null_with_setting_on_enables_and_audit_ReEnabled_true`.
  - Stale record, T3 (AU-03): `Get_by_id_of_stale_record_returns_stale_true`; `List_flags_stale_record`.
  - Disabled API user, T5 (inside `#if UMBRACO_HAS_API_USERS`): `Archived_api_user_cannot_get_client_credentials_token`: create an API user with `TestUsers.CreateApiUserAsync`, get a token (200), archive it, request again → not 200 (`invalid_client` or `invalid_grant`).
  - Validation: `Empty_userIds_returns_400`; `Five_hundred_and_one_userIds_returns_400_problem_details`; `RequireReason_with_whitespace_reason_returns_400`; `Reason_over_500_returns_400`; `Duplicate_ids_are_processed_once`.
  - Paging and ordering, P14: `List_with_unknown_orderBy_returns_400`; `List_with_take_zero_returns_no_items_and_total` (3 archived → `items == []`, `total == 3`); `List_with_take_1001_returns_400`; `List_with_negative_skip_returns_400`.
  - `archivedBy` shapes, P14 and Review Focus 5: `List_item_archived_by_system_has_null_archivedBy` (record inserted with a null performer); `List_item_archived_by_deleted_admin_has_id_and_null_name` (archive as admin X, delete X through `IUserService`, then `archivedBy == { id: X, name: null }`).
  - Mixed bulk, P9: `Non_admin_mixed_bulk_returns_per_user_statuses` (as a user manager, `userIds` = {self, invited, admin, ok editor} → `Self`, `Invited`, `NotPermitted`, `Archived` in request order; the admin is still enabled; one record).
  - Double submit, P6: `Two_parallel_archive_posts_for_one_user_give_one_record` (two `POST archived-users/archive` with the same id via `Task.WhenAll` → statuses {`Archived`, `AlreadyArchived`}, both 200, one record, one `Archived` audit row).
  - Round trip, P11 and Review Focus 2: `Reason_with_emoji_cjk_and_markup_round_trips_verbatim` (reason `"Left 🎉 Zoë 李 <b>x</b> & \"q\""` → `GET archived-users/{id}` returns it byte for byte; runs on SQLite here and on SQL Server nightly).
  - Behaviour: `Archive_one_user_returns_Archived_and_source_Manual`; `Archive_two_users_records_source_Bulk`; `Get_by_id_returns_archive_status`; `Config_returns_bound_settings`; `Cancelled_archive_returns_message`.
  - OpenAPI: `Package_openapi_document_lists_seven_operations_under_Archived_Users` (fetch the path recorded in S6). If Task 12-B is built, it changes this to eight, and the 401/403 tests gain the eighth route.
- [ ] **Step 3: Run** `dotnet test src/Umbraco.Community.ArchivedUsers.slnx --filter "FullyQualifiedName~ReasonValidator|FullyQualifiedName~ControllerConvention|FullyQualifiedName~Api"`. Expected: FAIL.
- [ ] **Step 4: Implement.** Base controller carries the three attributes plus `[Consumes("application/json")]`; actions use `CurrentUserKey(IBackOfficeSecurityAccessor)` and never read a performer, source or time from the request; validation errors return `ValidationProblem`/`ProblemDetails` 400, including `skip < 0`, `take` outside 0 to 1,000 and an unbound `orderBy` enum (P14). Controllers do **not** pre-authorise the batch; authorisation is per user in the service (design "Security review changes", AU-01 not-applied note), so a mixed bulk selection still gets per-user results. `ArchivedUsersComposer : IComposer` calls `AddArchivedUsersCore()` and registers the OpenAPI document per S6. The composer is the only `IComposer` in the package.
- [ ] **Step 5: Run.** Expected: "Failed: 0".
- [ ] **Checkpoint (Pascal commits):** `git add src/` / suggested message `feat: add archived users management API`.

### Task 12: Hide archived users from `/filter/user` (F6, Option A; build only if S1 is GO)

**Files:**
- Create: `Web/Filters/{ArchivedUserListFilter,UserListPaging,ArchivedUserListWarningClock}.cs`
- Modify: `Web/ArchivedUsersComposer.cs` (register filter scoped and `ArchivedUserListWarningClock` singleton; `Configure<MvcOptions>(o => o.Filters.AddService<ArchivedUserListFilter>())`)
- Test: `Tests/UserListPagingTests.cs`, `Tests/ArchivedUserListFilterTests.cs`, `IntegrationTests/Api/FilterUserTests.cs`

**Interfaces:**
- Consumes: `IArchivedUserService.GetArchivedKeysAsync` (Task 9), `IUserService.FilterAsync`, `IUserPresentationFactory`, the S1 notes on `Filter2`'s parameters.
- Produces (also used by Task 12-B):
  - `static class UserListPaging { (IReadOnlyList<T> Page, int Total) Apply<T>(IReadOnlyList<T> ordered, Func<T, Guid> key, ISet<Guid> drop, int skip, int take); HashSet<Guid> DropSet(IEnumerable<IUser> users, ISet<Guid> archivedKeys); }`. `DropSet` returns the keys that are archived **and** `UserState == Disabled` (AU-03, P12b).
  - `sealed class ArchivedUserListWarningClock { bool ShouldWarn(string key, DateTimeOffset now); }` (DI singleton; at most once per hour per key).

- [ ] **Step 1: Write the failing unit tests.**
  - `UserListPagingTests` (Review Focus 4): 25 users, 3 archived spread across positions 2, 11, 24 → `Total == 22` for every page; pages `(0,10)`, `(10,10)`, `(20,10)` concatenate to the 22 non-archived in original order; `skip == 22` → empty page, total 22; `skip == 100` → empty, total 22; `take == 0` → empty, total 22; all of a page's core slice archived → next non-archived users fill the page.
  - `UserListPagingTests.DropSet_keeps_archived_but_enabled_key` (AU-03): archived keys {A (Disabled), B (Active)} → `DropSet == {A}`.
  - `UserListPagingTests.DropSet_ignores_disabled_user_without_record`.
  - `ArchivedUserListFilterTests` (fake `ActionExecutingContext`): `Setting_off_calls_next_and_leaves_result_null`; `No_archived_users_calls_next` (fast path, `FilterAsync` not received); `Other_controller_calls_next`; `Same_controller_other_action_calls_next`; `Non_GET_method_calls_next` (AU-10, T9); `Api_user_with_HideArchivedForApiUsers_false_calls_next` (AU-10); `Api_user_with_HideArchivedForApiUsers_true_filters`; `Core_FilterAsync_failure_calls_next` (core then returns its own error); `Repository_exception_calls_next_and_logs_once` (AU-10, T8); `Presentation_factory_exception_calls_next` (AU-10); `Total_above_20000_calls_next_and_warns` (AU-13). The two API-user tests sit inside `#if UMBRACO_HAS_API_USERS`.
- [ ] **Step 2: Write the failing API tests** (`FilterUserTests`, 25 seeded users, 3 archived):
  - `Filter_user_excludes_archived_with_correct_total_across_three_pages`.
  - `Filter_user_honours_userStates_filter_and_orderBy` (order equals core's order minus archived keys).
  - `Filter_user_with_userGroupIds_hides_archived_disabled_members` (P7: the user group member list and the "Add users" picker call this; two archived members of `editor` are absent and `total` drops by 2).
  - `Filter_user_with_name_filter_hides_archived_match` (P7: `filter=demo.user0` matches 9 users, 2 archived → 7 items, `total == 7`).
  - `Filter_user_with_HideDisabledUsersInBackOffice_on_matches_core` (P7: set the core option in `CustomTestSetup`; the response equals the setting-off response, because core already hides every disabled user and the package drops nothing extra; `total` is not reduced twice).
  - `Filter_user_paging_with_archived_users_at_page_boundaries` (P7: archived users at positions 9, 10 and 19 with `take=10`; pages 0 to 2 concatenate to the non-archived set in core's order, `total` correct on each).
  - `Filter_user_honours_userKinds` (T9, AU-10; inside `#if UMBRACO_HAS_API_USERS`): with an API user seeded, `userKinds=Api` returns core's API-user set minus archived-and-disabled ones.
  - `Filter_user_with_setting_off_returns_archived_users_and_core_total`.
  - `Filter_user_shows_archived_but_enabled_user` (T3, AU-03): insert a record directly for an enabled user; it is in the response and counted in `total`.
  - `Filter_user_fails_open_when_table_is_missing` (T8, AU-10): drop `archivedUsersUser` in the test DB; response is 200 and equals core's (setting-off) response.
  - `Filter_user_as_api_user_returns_core_list_by_default` (AU-10; inside `#if UMBRACO_HAS_API_USERS`).
  - `Item_user_and_user_by_id_still_return_archived_user`.
  - `Current_user_endpoint_unaffected_for_archived_admin_viewer` (sanity).
  - `Non_admin_filter_still_hides_admins_and_superuser` (T15; core rule survives our short-circuit).
  - `CoreContractTests.Filter_user_action_name_is_Filter2` (`Tripwire`; the action-name match; v17 asserts `Filter`).
- [ ] **Step 3: Run** `dotnet test src/Umbraco.Community.ArchivedUsers.slnx --filter "FullyQualifiedName~UserListPaging|FullyQualifiedName~ArchivedUserListFilter|FullyQualifiedName~FilterUserTests"`. Expected: FAIL.
- [ ] **Step 4: Implement** per spec 7.2 "Action filter on `/filter/user`":
  - Match: act only when `ControllerTypeInfo == typeof(FilterUserFilterController)`, `ActionName == "Filter2"`, the route template from S1 matches, and `HttpMethods.IsGet(...)` (AU-10). Why-comment: a rename only stops hiding, which is core behaviour, and the contract test catches it.
  - Skip: setting off; API users unless `HideArchivedForApiUsers` (`#if UMBRACO_HAS_API_USERS`); no archived keys (fast path).
  - Bind: read `context.ActionArguments` by the parameter names S1 recorded, including `userKinds` (why-comment: model binding already ran, so we match core's binding exactly). Build `UserFilter` the way `Filter2` does at `release-18.3.0` (cite the SHA in a why-comment).
  - Fetch: `FilterAsync(currentUserKey, filter, 0, 20000, orderBy, orderDirection)`. If `Total > 20000`, warn through the clock and `await next()` (AU-13). Warn once per hour through the clock when the unfiltered total exceeds 5,000.
  - Drop and page: `UserListPaging.DropSet(items, archivedKeys)`, then `UserListPaging.Apply`; map items with `IUserPresentationFactory`; `context.Result = Ok(new PagedViewModel<UserResponseModel> { Total, Items })`.
  - Fail open: wrap everything after the method match in `try/catch (Exception)`; log at Error through the clock (user keys never) and `await next()` (AU-10).
  - No static fields: the last-logged times live in `ArchivedUserListWarningClock`.
- [ ] **Step 5: Run.** Expected: "Failed: 0".
- [ ] **Checkpoint (Pascal commits):** `git add src/` / suggested message `feat: hide archived users from the Users list and pickers`.

### Task 12-B: Option B fallback (build only if S1 is NO-GO; replaces Task 12)

Built instead of Task 12 when `docs/spikes.md` S1 says `**Gate:** NO-GO`. Design 7.2 "Option B in detail" is the spec.

**Files:**
- Modify first: `docs/design.md` and the workspace `design.md`: F6 says `/filter/user` API clients still see archived users; only the backoffice collection (and the pickers U5 lists as using it) hides them.
- Create: `Web/Controllers/ArchivedUsersUserListController.cs`, `Web/Filters/{UserListPaging,ArchivedUserListWarningClock}.cs` (as Task 12), `Client/src/user-collection/{archived-user-collection.repository.ts,register-swap.ts}`, `Client/test/register-swap.test.ts`
- Modify: `Core/Configuration/ArchivedUsersSettings.cs` and `Tests/ArchivedUsersSettingsTests.cs` (remove `HideArchivedForApiUsers`), `Web/ArchivedUsersComposer.cs`, `Client/src/index.ts`
- Test: `Tests/UserListPagingTests.cs` (Task 12's tests, unchanged), `IntegrationTests/Api/UserListTests.cs`

**Interfaces:**
- Consumes: `UserListPaging` and `ArchivedUserListWarningClock` exactly as Task 12 defines them; `IArchivedUserService.GetArchivedKeysAsync`; `IUserService.FilterAsync`; `IUserPresentationFactory`; S1's notes on core's `/filter/user` parameters; `archived-users/config` (Task 11).
- Produces:
  - `GET archived-users/user-list` on `ArchivedUsersUserListController : ArchivedUsersControllerBase` (inherits the three security attributes), query `skip`, `take`, `orderBy`, `orderDirection`, `userGroupIds`, `userStates`, `filter`, `userKinds` (bound with core's parameter types), response `PagedViewModel<UserResponseModel>`.
  - Client: `ArchivedUsersUserCollectionRepository` (registered under core's alias `Umb.Repository.UserCollection` after unregistering core's), `requestCollection(filter)` → `{ data: { items, total } }` in core's collection-repository shape. `registerUserCollectionSwap(extensionRegistry, config: { hideArchivedFromUserList: boolean }): boolean` in `register-swap.ts` (returns whether it swapped).

- [ ] **Step 1: Write the failing server tests** (`UserListTests`, same 25-user seed as Task 12):
  - `User_list_excludes_archived_with_correct_total_across_three_pages`; `User_list_with_userGroupIds_and_name_filter_hides_archived`; `User_list_take_zero_returns_total_only`.
  - `User_list_shows_archived_but_enabled_user` (AU-03).
  - `User_list_above_20000_returns_core_page_unfiltered` (AU-13; substitute `IUserService` returning `Total = 20001`).
  - `Unauthenticated_user_list_returns_401` and `User_without_Users_section_user_list_returns_403` (T14).
  - `Non_admin_user_list_hides_admins_and_superuser` (T15).
  - `User_list_honours_userKinds` (`#if UMBRACO_HAS_API_USERS`).
- [ ] **Step 2: Write the failing client tests** (`register-swap.test.ts`, a fake registry):
  - `swaps_only_when_hideArchivedFromUserList_is_true` (true → core alias unregistered, ours registered, returns true; false → registry untouched, returns false).
  - `repository_falls_back_to_core_filter_user_on_error` (our endpoint rejects with 500, then with 403 → the repository returns core's `/filter/user` result both times and logs one console warning without user data) (AU-10).
  - `repository_passes_core_filter_arguments_through` (skip, take, orderBy, userGroupIds, userStates, filter reach the request unchanged).
- [ ] **Step 3: Run** `dotnet test src/Umbraco.Community.ArchivedUsers.slnx --filter "FullyQualifiedName~UserListPaging|FullyQualifiedName~UserListTests"` and `cd src/Umbraco.Community.ArchivedUsers/Client && npm test`. Expected: FAIL.
- [ ] **Step 4: Implement the endpoint.** Same body as Task 12 Step 4's Fetch, Drop and page steps, as a controller action instead of a filter. Remove `HideArchivedForApiUsers` from the settings and its default test (pre-release, and it would do nothing under Option B).
- [ ] **Step 5: Implement the swap.** `index.ts`'s `onInit` fetches `archived-users/config` once, then calls `registerUserCollectionSwap`. The repository calls `GET archived-users/user-list`; on any error it calls core's `/filter/user` through core's own generated client. Why-comment: fail open (AU-10); a picker user without Users-section access gets a 403 from us and core's list from the fallback.
- [ ] **Step 6: Run.** Same commands. Expected: "Failed: 0" and Vitest "0 failed".
- [ ] **Step 7: Verify in the demo site.** Users list total and pages 2 and 3 are right with three archived users; the "Add users" picker and the Content user picker behave as `docs/spikes.md` U5 predicted for Option B; with `HideArchivedFromUserList: false`, no swap happens (DevTools Network shows `/filter/user`). Console: no errors.
- [ ] **Step 8: Knock-on changes, made in the same branch:**
  - Task 11: the 401/403 tests and `ControllerConventionTests` cover 8 routes; the OpenAPI test expects 8 operations.
  - Task 12: `CoreContractTests.Filter_user_action_name_is_Filter2` and the `ArchivedUserListFilter` tests are not built.
  - Task 13: none beyond `index.ts` (the archive actions don't depend on the swap).
  - Task 16: `pickers.spec.ts` asserts each picker as U5 recorded for Option B; any `/filter/user` API-level assertion is dropped; `uc1` still asserts absence from the Users list.
  - Task 17: README D1 section says `/filter/user` is unfiltered and lists which pickers hide archived users; `HideArchivedForApiUsers` is gone from the config table; the AU-10 note says API clients always see archived users.
  - Task 19: if S1 was NO-GO on 17 only, this task is built on `v17` alone and the README's supported-versions table says which line hides how.
- [ ] **Checkpoint (Pascal commits):** `git add docs/design.md src/` / suggested message `feat: hide archived users via a client collection repository swap`.

### Milestone review B: permissions and API (after Task 12 or 12-B)

Follow "Milestone reviews". This covers AU-01 to AU-04 and AU-10 to AU-16 in code. Nothing in M2 starts until it is closed.

- [ ] **Step 1: Dispatch both reviewers in parallel**, each with the repo path, `git diff <Milestone review A close commit>..HEAD`, `docs/design.md` sections 4, 7.2 and 9, `docs/spikes.md`, and this plan's Tasks 5 to 12-B:
  - `qa-reviewer` (model `sonnet`): are the tests adequate for the code (Review Focus 1 to 5, P6, P7, P9, P10, P14), do they assert behaviour rather than mocks where it matters, and are any skipped, ignored or provider-gated?
  - `security-analyst` (model `opus`), also given the private `security-review.md`: does every write path authorise per user (AU-01) including the disabled-performer rule, does the filter fail open and hide only archived-and-disabled users (AU-03, AU-10), are request hygiene, logging and SQL rules held (AU-12, AU-14, AU-16), and do T1, T2, T3, T5, T8 to T16 pass for the reasons claimed?
- [ ] **Step 2: Save** both JSON verdicts to the private workspace `reviews/` folder.
- [ ] **Step 3: Close.** Every Critical and High fixed under TDD and re-checked once by the reviewer that raised it. A `security-analyst` Critical is never waived. Expected: both verdicts `pass` or `pass-with-advisories`; Pascal has seen the advisories.

---

## M2: backoffice client

Before Task 13, load `umbraco-cms-backoffice-skills:umbraco-entity-actions`, `umbraco-modals`, `umbraco-conditions`, `umbraco-localization` and `umbraco-global-context`, and read `docs/spikes.md` S6.

### Task 13: API client, localisation, archive status context, conditions, confirm modal and editor actions (F1, F5, D6)

**Files:**
- Create in `Client/`: `openapi-ts.config.ts`, `src/api/` (generated, or the S6 fallback), `src/lang/{en,en-gb}.ts`, `src/archive-status/{archive-status.context.ts,archive-status.context-token.ts}`, `src/conditions/{predicates.ts,condition-inputs.ts,can-archive.condition.ts,is-archived.condition.ts}`, `src/modal/{archive-confirm.modal-token.ts,archive-confirm.element.ts}`, `src/entity-actions/{archive.action.ts,unarchive.action.ts}`, `src/results/summarise.ts`, `src/manifests.ts`, `vitest.config.ts`, `test/{predicates,condition-inputs,summarise}.test.ts`, `build/check-xss.sh` (repo root)
- Modify: `src/index.ts`, `.github/workflows/build.yml` (XSS check step)

**Interfaces:**
- Consumes: the Task 11 contract; the S6 refresh event.
- Produces:
  - `predicates.ts`: `canArchive(u: { isSelf: boolean; state: UserStateModel; isArchived: boolean; isStale: boolean; targetIsAdmin: boolean; targetIsSuperUser: boolean; performerIsAdmin: boolean }): boolean` (P12d; the admin inputs only hide the action, the server check is the control); `showNeverLoggedInHint(u: { state: UserStateModel; lastLoginDate: string | null; isArchived: boolean }): boolean`
  - `condition-inputs.ts` (P8, the condition glue as a pure function): `toCanArchiveInput(target: { unique: string; state: UserStateModel; userGroupUniques: string[] }, current: { unique: string; isAdmin: boolean }, status: { isArchived: boolean; isStale: boolean }, adminGroupUnique: string, superUserUnique: string): Parameters<typeof canArchive>[0]`. `can-archive.condition.ts` only observes the contexts and calls `canArchive(toCanArchiveInput(...))`.
  - `summarise.ts`: `summariseResults(results: UserArchiveResult[]): { done: number; skipped: number; skipReasons: Partial<Record<SkipReason, number>> }`, `type SkipReason = 'Self' | 'Invited' | 'AlreadyArchived' | 'NotArchived' | 'NotPermitted' | 'NotFound' | 'Cancelled' | 'CoreRefused'`
  - Global context `ArchivedUsers.GlobalContext.ArchiveStatus` with token `ARCHIVED_USERS_ARCHIVE_STATUS_CONTEXT`: `observeIsArchived(unique: string): Observable<boolean>`, `observeIsStale(unique: string): Observable<boolean>`, `refresh(unique: string): Promise<void>`, `config(): Promise<ArchivedUsersConfig>` (fetched once)
  - Modal token `ARCHIVED_USERS_ARCHIVE_CONFIRM_MODAL` (alias `ArchivedUsers.Modal.ArchiveConfirm`): data `{ mode: 'archive' | 'unarchive' | 'cleanUp'; count: number; sampleNames?: string[]; showNeverLoggedInHint?: boolean; requireReason: boolean }`, value `{ reason?: string }`. This modal is the only confirm the package uses; `UMB_CONFIRM_MODAL` is banned (AU-04)
  - Manifests with the exact aliases from spec 7.3: `ArchivedUsers.Condition.User.CanArchive`, `ArchivedUsers.Condition.User.IsArchived`, `ArchivedUsers.Modal.ArchiveConfirm`, `ArchivedUsers.EntityAction.User.Archive`, `ArchivedUsers.EntityAction.User.Unarchive`, `ArchivedUsers.Localization.En`, `ArchivedUsers.Localization.EnGb`

- [ ] **Step 1: Write the failing Vitest tests.**
  - `predicates.test.ts`: `canArchive` false for self, false for Invited, false when archived and not stale, **true when archived but stale** (AU-03), true for an active or disabled other user, false for the superuser target, false for an admin target when `performerIsAdmin` is false, true for an admin target when it is true (P12d); `showNeverLoggedInHint` true only for Disabled + `lastLoginDate === null` + not archived; false for Active never-logged-in (spec D6 says disabled).
  - `condition-inputs.test.ts` (P8): self detected by `unique` match; `targetIsAdmin` true only when `userGroupUniques` contains the admin group; `targetIsSuperUser` true only for the superuser's unique; status passed through.
  - `summarise.test.ts`: `[Archived, Archived, Self, CoreRefused:CannotDisableInvitedUser]` → `{ done: 2, skipped: 2, skipReasons: { Self: 1, CoreRefused: 1 } }`; `Unarchived` counts as done; empty list → zeros.
- [ ] **Step 2: Run** `cd src/Umbraco.Community.ArchivedUsers/Client && npm test`. Expected: FAIL, modules not found.
- [ ] **Step 3: Implement the pure modules.** Run `npm test`. Expected: the three files pass, "0 failed".
- [ ] **Step 4: Generate the API client.** Start the demo site, then `npm run generate:api` (script `openapi-ts`, input = the S6 document URL, output `src/api/generated`). Expected: `src/api/generated/sdk.gen.ts` exists and `npx tsc --noEmit` passes.
- [ ] **Step 5: Implement the elements and manifests.**
  - Localisation: keys under `archivedUsers_*` in both files, covering every label in spec F1 to F5 and D6, including the D6 text verbatim ("This user has never logged in. You can delete them with the core Delete action instead."), the reason helper text verbatim ("Do not record sensitive personal details. Use a ticket reference.", AU-09), "Enabled - archive record is stale" (AU-03), "Removed user", "System", one message per `SkipReason`, and a fallback "Core refused: {0}". No key's value contains user data; user data only ever arrives as args.
  - Modal element: `uui-textarea` with `maxlength=500`, a visible label, the AU-09 helper text as its description, `required` mirroring `requireReason`; confirm disabled while `requireReason && !reason.trim()`; confirm button `color="danger"`; shows count, up to 10 sample names rendered with `repeat(sampleNames, n => n, n => html\`<li>${n}</li>\`)`, and the D6 hint when set. Names and reason are rendered as text bindings only, never via `htmlString` or `UMB_CONFIRM_MODAL` (AU-04).
  - Conditions read `UMB_USER_WORKSPACE_CONTEXT` and `UMB_CURRENT_USER_CONTEXT`, plus the status context; they re-evaluate when `observeIsArchived` or `observeIsStale` emits. The admin group and superuser uniques come from the constants S6 recorded; if the client exposes neither, read them from the server config endpoint and record that in `docs/spikes.md` S6. `IsArchived` shows Unarchive for any record, stale or not.
  - Archive action: open modal (`mode: 'archive'`, `count: 1`, hint from `showNeverLoggedInHint`); POST archive; show a notification from `summariseResults` with plain-text `message` only, per the S6 note on how `peek` renders it (cancel and core-refused messages verbatim from the response, rendered as text, AU-04); `refresh(unique)`; dispatch the S6 reload event.
  - Unarchive action: same, `mode: 'unarchive'`, POST unarchive without `enable`.
- [ ] **Step 6: XSS guard (AU-04, T7, P11).** Create `build/check-xss.sh` (run from the repo root, `set -euo pipefail`, exit 1 on any match) with two checks over `src/Umbraco.Community.ArchivedUsers/Client/src`:
  - Banned sinks, one `grep -rnE` with the pattern `unsafeHTML|unsafeSVG|unsafeStatic|innerHTML|outerHTML|insertAdjacentHTML|DOMParser|createContextualFragment|document\.write|setHTMLUnsafe|srcdoc|\beval\(|new Function\(|UMB_CONFIRM_MODAL|htmlString`.
  - Non-literal localisation keys: `grep -rnE "\.term\(\s*[^'\"[:space:]]"` (the first argument of `.term(` must start with a quote).
  Call it from `build.yml` after the client build. Prove each check bites: add a scratch file `Client/src/_xss_probe.ts` containing ``this.localize.htmlString('k', name)``, run the script, expect exit code 1 and the probe line printed; replace the content with ``this.localize.term(key)``, expect exit code 1 again; then ``const f = new Function('x')``, exit 1; delete the probe and run again. Expected: exit code 0, no output.
- [ ] **Step 7: Verify.** `npm run build && npm test`. Expected: build writes `../wwwroot/index.js`, tests pass. Start the demo site; open `demo.user05`'s editor as admin: **Archive** shows, **Unarchive** doesn't; archive with reason "Left 24 Oct, SUP-123"; the user editor now shows **Unarchive** and the user is Disabled; open your own user: no **Archive**. Console: no errors.
- [ ] **Checkpoint (Pascal commits):** `git add src/Umbraco.Community.ArchivedUsers/Client src/Umbraco.Community.ArchivedUsers/wwwroot .github/workflows/build.yml build/check-xss.sh` / suggested message `feat: archive and unarchive from the user editor`.

### Task 14: Bulk actions and "Archive disabled users" (F2, F3)

**Files:**
- Create in `Client/src/`: `bulk-actions/{archive.bulk-action.ts,unarchive.bulk-action.ts}`, `collection-action/archive-disabled.action.ts`
- Modify: `src/manifests.ts`, `lang/*.ts`

**Interfaces:**
- Consumes: modal token, `summariseResults`, status context (Task 13).
- Produces aliases: `ArchivedUsers.EntityBulkAction.User.Archive` (`forEntityTypes: ['user']`, `UMB_COLLECTION_ALIAS_CONDITION` match `Umb.Collection.User`), `ArchivedUsers.EntityBulkAction.User.Unarchive` (match `ArchivedUsers.Collection.ArchivedUser`), `ArchivedUsers.CollectionAction.User.ArchiveDisabled` (kind `button`, `api` per S6, collection condition `Umb.Collection.User` plus `UMB_IN_MODAL_CONDITION_ALIAS` with `match: false`).

- [ ] **Step 1: Write the failing test.** Extend `summarise.test.ts`: `formatSummary(summary, 'archive', localize)` returns "3 archived, 2 skipped" plus one line per reason in a fixed order (Self, Invited, AlreadyArchived, NotArchived, NotPermitted, NotFound, Cancelled, CoreRefused) (P12c). Tests: `formats_every_skip_reason_in_fixed_order` (one of each of the eight reasons → eight reason lines in that order, each from its own localisation key); `omits_reasons_with_zero_count`; `unarchive_summary_says_unarchived` (mode `unarchive` → "2 unarchived, 1 skipped" with `NotArchived`); `lines_contain_no_names` (results carry only ids and statuses, so no line contains anything but counts and localised text) (P8). Signature `formatSummary(s: ReturnType<typeof summariseResults>, mode: 'archive' | 'unarchive', t: (key: string, ...args: unknown[]) => string): string[]`.
- [ ] **Step 2: Run** `npm test`. Expected: FAIL, `formatSummary` not exported.
- [ ] **Step 3: Implement** `formatSummary`, then the three actions. Bulk archive: modal `count = selection.length`; POST archive with the selection; notification lines from `formatSummary`; reload the collection via the S6 event. Clean-up: GET preview; if `count === 0`, open the package modal in an info state saying there are none and offer only Close (never `UMB_CONFIRM_MODAL`, AU-04); else modal `mode: 'cleanUp'` with `count` and `sampleNames` passed as an array, never joined into a string; POST clean-up; summary; reload. `formatSummary` lines contain counts and localised reasons only, never names.
- [ ] **Step 4: Verify.** `npm run build && npm test` pass. In the demo site as admin: select 3 users including yourself and the invited user → "1 archived, 2 skipped" with reasons Self and Invited. **Archive disabled users** says "10 disabled users will be archived" with 10 names, then archives them; pressing it again says there are none. Open a user group's "Add users" picker: the button is absent. Console: no errors.
- [ ] **Checkpoint (Pascal commits):** `git add src/Umbraco.Community.ArchivedUsers/Client src/Umbraco.Community.ArchivedUsers/wwwroot` / suggested message `feat: bulk archive and archive all disabled users`.

### Task 15: Archived users view (F4)

**Files:**
- Create in `Client/src/archived-view/`: `manifests.ts`, `archived-user-collection.repository.ts`, `archived-user-collection.context.ts`, `archived-user-table.element.ts` (only if S6 found no `table` kind), `map-item.ts`, `debounce.ts`, `archived-user-name-cell.element.ts`; `test/map-item.test.ts`, `test/debounce.test.ts`, `test/name-cell.render.test.ts`

**Interfaces:**
- Consumes: Task 11 `GET archived-users`, `UMB_EDIT_USER_WORKSPACE_PATH_PATTERN`.
- Produces: `type ArchivedUserCollectionItem = { unique: string; entityType: 'user'; name: string; email: string; archivedUtc: string; archivedByName: string | null; archivedBySystem: boolean; reason: string | null; lastLoginDate: string | null; stale: boolean }`; `mapItem(m: ArchivedUserResponseModel): ArchivedUserCollectionItem`; aliases `ArchivedUsers.MenuItem.ArchivedUsers` (menu `Umb.Menu.UserManagement`, `entityType: 'archived-users-root'`, weight 190), `ArchivedUsers.Workspace.Root`, `ArchivedUsers.WorkspaceView.Root.Collection`, `ArchivedUsers.Collection.ArchivedUser`, `ArchivedUsers.Repository.ArchivedUserCollection`, `ArchivedUsers.CollectionView.ArchivedUser.Table`. `ArchivedUserNameCellElement` (`archived-user-name-cell`, a plain `LitElement` with no `@umbraco-cms/backoffice` import so it renders under Vitest): properties `name: string`, `stale: boolean`, `staleLabel: string`; renders the name and, when `stale`, a `uui-tag` with `staleLabel`, both as text bindings.

- [ ] **Step 1: Write the failing test.** `map-item.test.ts`: maps `user.id`, `user.name`, `user.email`, `user.lastLoginDate`; `archivedBy: null` → `archivedBySystem: true, archivedByName: null`; `archivedBy: { id, name: null }` → `archivedBySystem: false, archivedByName: null` (rendered as "Removed user", Review Focus 5); `entityType === 'user'` so the Unarchive bulk action applies; `stale: true` maps through (AU-03).
  - `name-cell.render.test.ts` (P11, AU-04; Vitest `environment: 'happy-dom'` for this file): render `archived-user-name-cell` with `name = '<img src=x onerror=window.__xss=1>'`, `stale = true`, `staleLabel = 'Enabled - archive record is stale'`; the shadow root contains no `img` element, its text contains the literal name, the tag is present, and `window.__xss` is undefined. Same with `stale = false`: no tag.
  - `debounce.test.ts` (AU-13): `debounce(fn, 300)` called 5 times within 100 ms calls `fn` once, 300 ms after the last call (fake timers). Signature `debounce<T extends unknown[]>(fn: (...a: T) => void, ms: number): (...a: T) => void` in `src/archived-view/debounce.ts`.
- [ ] **Step 2: Run** `npm test`. Expected: FAIL.
- [ ] **Step 3: Implement** `mapItem`, the repository (`requestCollection({ skip, take, filter, orderBy, orderDirection })` → `{ items, total }`, default `archivedUtc` descending), the collection context with `requestItemHref` built from `UMB_EDIT_USER_WORKSPACE_PATH_PATTERN.generateAbsolute({ unique })`, the table columns (name, email, archived, archived by, reason, last login) with sortable name and archived columns, the name column rendered by `archived-user-name-cell` (the stale tag "Enabled - archive record is stale" when `stale`, AU-03), a search box whose input is debounced 300 ms (AU-13), and an empty state using the localised "No archived users". Every cell is a text binding.
- [ ] **Step 4: Verify.** `npm run build && npm test` pass. In the demo site: **Archived users** sits under **Users** in the menu; table lists the users archived earlier with reason and "archived by"; search "user05" finds one; sorting by name works; clicking a row opens the core user editor; select two rows → **Unarchive** → they leave this view, show again in the Users list, and are still Disabled. Log in as Morgan Manager (non-admin): an archived Alex Admin is not listed. Console: no errors.
- [ ] **Checkpoint (Pascal commits):** `git add src/Umbraco.Community.ArchivedUsers/Client src/Umbraco.Community.ArchivedUsers/wwwroot` / suggested message `feat: add the Archived users view`.

---

## M3: verification and release

### Task 16: End-to-end and accessibility tests

Load `umbraco-cms-backoffice-testing-skills:umbraco-e2e-testing` and `umbraco-playwright-testhelpers` first.

**Files:**
- Create: `tests/e2e/{package.json,package-lock.json,playwright.config.ts,.env.example}`, `tests/e2e/specs/{uc1-offboard,uc2-clean-up,uc2-bulk,uc4-returning,pickers,a11y,xss,session}.spec.ts`, `tests/e2e/support/console-guard.ts`

**Interfaces:**
- Consumes: the demo site at `URL` from `.env` (default `https://localhost:44331`), admin and Morgan Manager credentials from env vars, never committed. Task 4a's U1 result (token mode and lifetime). Task 2's U5 list (which pickers call which endpoint). S0's back-office token endpoint path.
- Produces: `consoleGuard(page): { assertNoErrors(): void }` in `support/console-guard.ts` (collects `console` errors and `pageerror` events; every spec calls `assertNoErrors()` at the end).

- [ ] **Step 1: Write the specs.** Each creates its own users through `umbracoApi` with a unique run suffix, cleans up its records via the package API, and ends with `consoleGuard(page).assertNoErrors()`.
  - `uc1-offboard`: archive "Jo <suffix>" from the editor with reason "Left 24 Oct, SUP-123"; assert Jo is absent from the Users list (search), present in Archived users with that reason and "archived by" the admin; reason `<img src=x onerror=window.__xss=1>` renders as literal text and `window.__xss` is undefined (Review Focus 2). Added assertions (P8):
    - the Archive modal shows the AU-09 helper text "Do not record sensitive personal details. Use a ticket reference.";
    - on a disabled, never-logged-in user, the modal shows the D6 text "This user has never logged in. You can delete them with the core Delete action instead.";
    - the actions menu on the admin's own user has no **Archive**; on an invited user, no **Archive**;
    - in a user group's "Add users" picker modal, the **Archive disabled users** button is absent.
  - `uc2-clean-up`: read the expected count from `GET archived-users/clean-up/preview`; click **Archive disabled users**; assert the dialog shows that count; confirm; preview count is then 0.
  - `uc2-bulk` (P8): logged in as Morgan Manager (non-admin), select Morgan's own row, an invited user and one ordinary editor, choose **Archive**, confirm; the notification reads "1 archived, 2 skipped" with the Self and Invited reason lines; the editor is in Archived users. The admin case can't be selected in the UI (core hides admins from non-admins), so it is covered by Task 11's `Non_admin_mixed_bulk_returns_per_user_statuses`; a comment in the spec says so.
  - `uc4-returning`: unarchive from the Archived view; user is back in the Users list, state Disabled; enable via core; user is Active and not archived.
  - `pickers` (P7, U5): archive one editor. (a) Open a user group, "Add users": the archived editor is not offered; an active editor is. (b) Create a document type with a User Picker property and a document of that type through `umbracoApi`; open the document, open the user picker: the archived editor is not offered. Both assertions follow `docs/spikes.md` U5: where U5 recorded that a surface does **not** call `/filter/user` (or, under Option B, doesn't use the swapped repository), the spec asserts the user **is** offered and links the README line that documents it. No console errors on either surface.
  - `a11y`: `@axe-core/playwright` on the Archive confirm modal and the Archived users view, WCAG 2.2 AA tags (`wcag2a`, `wcag2aa`, `wcag21aa`, `wcag22aa`): zero violations. Keyboard-only: Tab reaches the reason field and the confirm button, Escape closes the modal, focus returns to the actions menu.
  - `xss` (T6, AU-04, P11): create a disabled user named `<img src=x onerror=window.__xss=1>` (and a second one `"><svg onload=window.__xss=1>`), archive them with the same strings as reason. Also:
    - a hostile performer: an admin whose display name is `<img src=x onerror=window.__xss=2>` archives a third user, so "archived by" carries the hostile name;
    - a hostile email: create a user with email `"><svg/onload=window.__xss=3>"@example.test`; if core's user create rejects it, assert the rejection (status 400) and record that in the spec comment, since then no package surface can receive it.
    Register `page.on('dialog', d => { throw … })`. Visit, in order: the user's editor and the Archive confirm (single); **Archive disabled users** with both in the sample list; the Archived users table (name, email and "archived by" columns); the post-action toast; the bulk summary toast. On each: `expect(await page.locator('img[src="x"], svg[onload]').count()).toBe(0)`, `expect(await page.evaluate(() => (window as any).__xss)).toBeUndefined()`, and the literal text is visible. The stale tag next to a hostile name is covered by Task 15's `name-cell.render.test.ts` (a stale record can't be produced through the UI). (The v0.2 history tab joins this spec in v0.2.)
  - `session` (T4, AU-02): two browser contexts. Context B logs in as a fresh editor and loads `/umbraco`. Context A (admin) archives that editor. Then in B: (a) a token refresh (`page.request.post` to the back-office token endpoint with `grant_type=refresh_token`, the path recorded in S0) is not 200; (b) `page.goto('/umbraco')` ends on the login screen, not the backoffice; (c) the security stamp changed is covered by Task 7's `Archive_changes_security_stamp`; (d) per U1: a Management API call with B's last access token, if self-contained tokens were found, is recorded as working until expiry (assert and document the observed result, not a guess). (e) P9: a package `POST archived-users/archive` sent with B's last access token, targeting another editor, returns `NotPermitted` for that editor (or 401), and that editor stays enabled. Runs at the floor and at latest via Task 17's nightly matrix.
- [ ] **Step 2: Run against the current build.** `cd tests/e2e && npm ci --ignore-scripts && npx playwright install chromium && npx playwright test`. Expected: every spec passes, "0 failed" (P13d: no fixed count). Any failure is a bug in Tasks 13 to 15 (or Task 7 for `session`): fix it there (TDD), rebuild, re-run.
- [ ] **Checkpoint (Pascal commits):** `git add tests/e2e` / suggested message `test: add end-to-end and accessibility specs for v0.1 use cases`.

### Task 17: Docs, CI and release prep for `18.0.0-alpha.1`

**Files:**
- Create: `CLAUDE.md`
- Modify: `README.md`, `.github/workflows/{build,nightly,publish}.yml`

- [ ] **Step 1: README.** Sections:
  - What "archived" means (soft state on Disabled, never deletes). Install (`dotnet add package Umbraco.Community.ArchivedUsers --prerelease`).
  - Config keys and defaults from spec 7.4 (v0.1 keys only; `HideArchivedForApiUsers` only if Task 12, not 12-B, was built).
  - D1 side effect (pickers never offer archived users; `/item/user` and `/user/{id}` still resolve them; the list of surfaces from `docs/spikes.md` U5). Effect on `/filter/user` integrations and `HideArchivedForApiUsers` (AU-10), or under Option B the Task 12-B wording. The `HideDisabledUsersInBackOffice` interaction. D6 (core Delete works for never-logged-in users).
  - GDPR retention note (D7 and AU-09: the site owner is the controller; what is stored; lawful basis legitimate interest in access-control evidence; audit rows outlive users by design, no FK; the reason field must not hold sensitive personal details; retention purge and per-user name erasure arrive in v0.2 as `Audit:RetentionDays` and an erasure method, so no raw SQL is documented, AU-07).
  - Security notes: hiding is not access control; settings must be identical on every node, `RequireReason` above all (AU-14b); don't relax `BackOfficeTokenCookie:SameSite` (AU-14); core does not revoke the superuser's tokens on disable and the package never targets the superuser (review R6); if U1 found self-contained access tokens, "an archived user's in-flight access token keeps working until it expires, at most N minutes" (review R2); **an archived user can't archive anyone, but two admins archiving each other at the same instant can both end up disabled; the superuser is never a package target, so keep it enabled as the recovery account** (P9 accepted risk).
  - Uninstall (tables stay). Supported versions table (`main` 18.x `[18.3.0, 19.0.0)`, `v17` 17.x `[17.8.0, 18.0.0)`, with one line on why: first releases where core refuses disabled users at sign-in, and above the 27 Oct 2026 advisories).
  - A "Security" pointer to `SECURITY.md` for private reporting. Nothing from `security-review.md` is quoted.
- [ ] **Step 2: CLAUDE.md.** Same sections as Forms Audit Trail's (branches and versioning, commands, layout, behaviours to preserve, releasing), rewritten for this package. Behaviours to preserve: the Global Constraints that a future edit could break (hide switch off = core, unarchive deletes before enabling inside one scope, the per-user `ArchiveUserLocks` lock and the disabled-performer rule, `Core/` line-agnostic, visibility mirrors core, **the service authorises every write because `IUserService` does not (AU-01)**, the filter hides only archived-and-disabled users and fails open (AU-03, AU-10), GET never writes, floors never below 17.8.0 / 18.3.0 (AU-02), aliases permanent, Gating contract tests are never edited to pass, the repo is public so review findings never go in it).
- [ ] **Step 3: CI.**
  - `build.yml` already has the floor and latest legs (Tasks 1 and 3), the drift check, the XSS check and the PR-check jobs. No change beyond confirming each `uses:` is SHA-pinned.
  - `nightly.yml` (from Task 5): add job `e2e` as a matrix over `leg: [floor, latest]`. The floor leg restores the demo site locked at `UmbracoDemoVersion=18.3.0`; the latest leg restores with `-p:UmbracoDemoVersion=18.* --force-evaluate -p:RestoreLockedMode=false` (P1). Each leg starts the demo site and runs Playwright (T4 at the floor and at latest).
  - `publish.yml`: pack `src/Umbraco.Community.ArchivedUsers/Umbraco.Community.ArchivedUsers.csproj` only (tests and demo are `IsPackable=false`); run `bash build/check-vulnerable.sh` before packing; push with NuGet Trusted Publishing (`permissions: id-token: write`, the `NuGet/login` action pinned by SHA, policy configured on nuget.org by Pascal for this repo and both package IDs), no long-lived API key secret (AU-15).
- [ ] **Step 4: Verify locally.** `dotnet build src/Umbraco.Community.ArchivedUsers.slnx -c Release && dotnet test src/Umbraco.Community.ArchivedUsers.slnx -c Release && dotnet pack src/Umbraco.Community.ArchivedUsers/Umbraco.Community.ArchivedUsers.csproj -c Release -o ./artifacts`. Expected: all tests pass; exactly one `Umbraco.Community.ArchivedUsers.18.0.0-alpha.1.nupkg`. `unzip -l artifacts/*.nupkg | grep -E "staticwebassets/(index.js|umbraco-package.json)"` lists both; `unzip -p artifacts/*.nupkg '*.nuspec' | grep -E 'Umbraco.Cms.(Web.Common|Api.Management)'` shows both with `version="[18.3.0, 19.0.0)"`. Then `bash build/check-vulnerable.sh`. Expected: exit 0. Prove it bites: temporarily add a package version with a known advisory to a scratch csproj in the scratch folder, point the script at it, expect exit 1; remove it. `grep -rn "uses: " .github/workflows | grep -vE "@[0-9a-f]{40}"` prints nothing (all actions SHA-pinned). `ls docs/` prints only `design.md`, `plan.md` and `spikes.md`.
- [ ] **Step 4b (Pascal): Trusted Publishing.** On nuget.org, add a Trusted Publishing policy for `PascalA94/Umbraco.Community.ArchivedUsers`, workflow `publish.yml`, covering both package IDs.
- [ ] **Checkpoint (Pascal commits):** `git add README.md CLAUDE.md .github/` / suggested message `docs: document v0.1 behaviour, GDPR retention and CI`.

### Task 18: Exercise it in the backoffice

No new code unless a defect is found (then fix it under TDD in the owning task's files, and re-run that task's verification).

- [ ] **Step 1: Fresh run.** Delete `demo/.../umbraco/Data/`, start the demo site with `dotnet run --project demo/Umbraco.Community.ArchivedUsers.DemoSite 2>&1 | tee ~/Desktop/archived-users-v0.1-server.log`. Open the backoffice with DevTools open on Console and Network (filter "Fetch/XHR"). Keep both visible throughout.
- [ ] **Step 2: Happy paths (as admin).** UC1 archive one user with a reason; UC2 bulk archive 3; UC2 **Archive disabled users**; F4 search, sort, open a row; UC4 unarchive one from the editor and two from the Archived view; D6 hint shows on a disabled never-logged-in user and Archive still works; Users list total drops and paging stays right on page 2 and 3.
- [ ] **Step 3: Negative paths.** Archive self (not offered); invited user (not offered); as Morgan Manager, try the admin (no Archive action if the admin's editor opens at all, P12d; `POST archived-users/archive` with Alex's id from DevTools returns `NotPermitted`, and Alex is still enabled; the same with a random GUID also returns `NotPermitted`); as Alex Admin, `POST archived-users/archive` with the superuser's key returns `NotPermitted`; `POST archived-users/unarchive` with `{"userIds":[…],"enable":true}` while `UnarchiveEnablesUser` is false returns 400; a form-encoded POST returns 415; `RequireReason: true` in appsettings, restart, confirm disabled with blank or whitespace reason, and the API returns 400; a 501-char reason via DevTools returns 400; `GET archived-users?take=1001` returns 400; log out and call `GET archived-users` → 401.
- [ ] **Step 4: Edge paths** (P15: one checkbox per scenario).
  - [ ] Double-click Confirm: one record and one audit row (`sqlite3` on the demo SQLite file: `SELECT COUNT(*) FROM archivedUsersAudit WHERE userKey = '<key>'` prints 1).
  - [ ] Enable an archived user via core's Enable: record gone, a `CoreEnable` row with your key as performer.
  - [ ] Delete a never-logged-in archived user via core Delete: record gone, audit rows remain.
  - [ ] Archive then rename a user: the Archived view shows the new name.
  - [ ] Archive a user while logged in as them in a private window, then reload that window: signed out, no new token.
  - [ ] Archived performer (P9): log in as Alex Admin in a private window; as admin, archive Alex; from Alex's window, `POST archived-users/archive` for an editor from DevTools returns 401 or `NotPermitted`, and the editor stays enabled.
  - [ ] Stale record: stop the site, `UPDATE umbracoUser SET userDisabled = 0` for one archived demo user **in the throwaway SQLite file only**. Before restarting, note the plan: on start, reconciliation removes the record. Start the site: the record is gone and a `Reconcile` row exists. (To see the stale flag itself, make the same change while the site runs, then reload the Users list, which shows the user, and the Archived view, which flags it stale.)
  - [ ] Fail open: rename `archivedUsersUser` in the SQLite file, reload the Users list (200, core's list, one Error in the log), rename it back.
  - [ ] `HideArchivedFromUserList: false`, restart: archived users show in the Users list exactly as core would.
  - [ ] `HideDisabledUsersInBackOffice: true`: clean-up still counts server-side.
  - [ ] `UnarchiveEnablesUser: true`: a user that was enabled before archive comes back enabled; one that was disabled before stays disabled.
- [ ] **Step 5: Watch the signals.** Expected: zero console errors or warnings from `ArchivedUsers.*`; no failed requests other than the deliberate 400/401/403/415s; server log has no Error level entries other than the deliberate fail-open one, no unhandled exceptions, and no reason text or user names (`grep -c "Left 24 Oct" ~/Desktop/archived-users-v0.1-server.log` prints 0, AU-09); `grep -iE "error|exception" ~/Desktop/archived-users-v0.1-server.log` shows only the deliberate cases.
- [ ] **Step 6: Record.** Write the checklist with pass/fail per line into the PR description. Say plainly anything not verified (for example load-balanced hosting, which is v1.0). Run the full suites one last time: `dotnet test src/Umbraco.Community.ArchivedUsers.slnx -c Release`, `npm test` in `Client/`, `npx playwright test` in `tests/e2e`. Expected: "0 failed" in each. The PR's `build`, `secrets` and `deps` checks are green.

### Milestone review C: before release (`18.0.0-alpha.1`)

Follow "Milestone reviews". The release waits for this review.

- [ ] **Step 1: Dispatch `qa-reviewer`** (model `sonnet`) for a whole-branch review: `git diff main..feat/v0.1`, `docs/design.md`, this plan, `docs/spikes.md`, the PR's check results and the Task 18 checklist. Question: does the branch do what the design says for v0.1, are the Review Focus items and P1 to P15 pinned by tests that actually run, and is anything in the repo that must stay private (`ls docs/`, `git log --all --stat -- docs/` show no `security-review.md`, `plan-review.md` or review reports)?
- [ ] **Step 2: Save** the verdict to the private workspace `reviews/` folder. Fix blocking findings under TDD and re-check once.
- [ ] **Step 3 (Pascal): Release.** Merge the PR, tag `v18.0.0-alpha.1` on the merge commit, push the tag, `gh release create v18.0.0-alpha.1 --prerelease --generate-notes`. Marketplace listing stays off until v1.0.

### Task 19: Port to the `v17` branch (`17.0.0-alpha.1`)

**Files:**
- Modify on branch `port/v17` (PR into `v17`): RCL csproj, `Directory.Build.props`, `Client/package.json`, `Client/package-lock.json`, `Client/public/umbraco-package.json`, `Web/Filters/ArchivedUserListFilter.cs`, `Web/OpenApi/ArchivedUsersOpenApi.cs`, `Client/src/api/generated/**`, `IntegrationTests` csproj, `demo/` csproj, `README.md`, `CLAUDE.md`, `.github/workflows/*.yml`

**Interfaces:**
- Consumes: the `UMBRACO_HAS_API_USERS` constant from `Directory.Build.props` (Task 1); every `UserKind.Api` use already sits inside `#if UMBRACO_HAS_API_USERS` (Tasks 3, 11, 12, 12-B), and none is in `Core/`.

- [ ] **Step 1: Branch.** `git switch -c v17 v18.0.0-alpha.1 && git push -u origin v17` (Pascal), then `git switch -c port/v17`. The ruleset from Task 1 already covers `v17`, so the port reaches it by PR.
- [ ] **Step 2: Retarget.** `Umbraco.Cms.Web.Common` and `Umbraco.Cms.Api.Management` `[17.8.0, 18.0.0)`; npm `@umbraco-cms/backoffice ^17.8.0` (still a `devDependency` and Vite external); all four version files `17.0.0-alpha.1`; `UmbracoTestVersion` and `UmbracoDemoVersion` defaults `17.8.0`; the latest legs in `build.yml` and `nightly.yml` use `17.*` (unlocked, P1); regenerate and commit the lock files at the floor; demo site on the 17.8 template (`dotnet new install Umbraco.Templates::17.8.0` to compare if needed).
- [ ] **Step 3: Gate, spikes on 17.** Run `dotnet test src/Umbraco.Community.ArchivedUsers.IntegrationTests -c Release` at the floor and at the `17.*` leg. `CoreContractTests` cover S2, S4, U3 and the AU-01 tripwires on 17, with the same Gating, Tripwire and Recording rules as Task 3 Step 6. `MigrationTests` covers S5 on SQLite; the nightly `integration-sqlserver` job on the PR covers SQL Server. Re-read Task 2 Step 5's 17.8 S1 result and Task 4a's 17.8 U1 and U4 notes in `docs/spikes.md`.
  - `UserKind.Api` on 17.8 (P13a): if it doesn't exist, remove `UMBRACO_HAS_API_USERS` from `Directory.Build.props` on this branch only; the API-user branches of the filter and T5 compile out, and the README says so. Expected: `dotnet build` succeeds either way.
  - Expected: all green and S1 GO. If S1 was NO-GO on 17, build Task 12-B on this branch only and document the line difference in the README.
- [ ] **Step 4: Per-line code (only in `Web/`, `Client/` and build files).** `ArchivedUserListFilter`: match action name `Filter` instead of `Filter2` (and the contract test asserts `Filter`), still GET-only, fail-open and archived-and-disabled, no `userKinds`, build `UserFilter` as core's `Filter` does at `release-17.8.0` (cite the SHA). OpenAPI: Swashbuckle registration (`SwaggerGenOptions.SwaggerDoc("archived-users", …)` plus the operation-id handler core packages use); regenerate the client from the Swashbuckle document path; fix any `@umbraco-cms/backoffice` 17.x typing differences (imports and condition constants).
  - **Core changes (P13b):** if 17 needs a change in `Core/`, stop. Make it on `main` first (TDD, its own PR), then merge or cherry-pick it to `v17`. A behaviour that genuinely differs per line goes in `Web/` behind an interface, never in `Core/`.
- [ ] **Step 5: Verify parity.** `git diff v18.0.0-alpha.1 -- src/Umbraco.Community.ArchivedUsers/Core` prints nothing. `dotnet build ... && dotnet test ...` (as Task 17 Step 4, with `17.0.0-alpha.1` and `[17.8.0, 18.0.0)` for both core packages in the nuspec, and `bash build/check-vulnerable.sh` exit 0). `npm run build && npm test` pass; the CI drift check (`git diff --exit-code -- src/Umbraco.Community.ArchivedUsers/wwwroot`, Task 1) is green on the PR (P13c). `cd tests/e2e && npx playwright test` against the 17 demo site: every spec passes, "0 failed" (P13d).
- [ ] **Step 6: Exercise.** Repeat Task 18 Steps 1 to 5 on the 17 demo site. Record the results in the PR description.
- [ ] **Checkpoint (Pascal commits):** `git add -A` / suggested message `feat: port Archived Users v0.1 to Umbraco 17`. Pascal pushes `port/v17` and opens a PR against `v17`.
- [ ] **Step 7: Milestone review C for `v17`.** Dispatch `qa-reviewer` (model `sonnet`) with `git diff v18.0.0-alpha.1..port/v17`, the design's section 4 Versions and 10, and this task. Question: is every difference from `main` a per-line necessity outside `Core/`, and do the 17 legs test what Task 3 and Task 12 test on 18? Save the verdict to the private `reviews/` folder; fix blocking findings and re-check once.
- [ ] **Step 8 (Pascal): Release.** Merge the PR, tag `v17.0.0-alpha.1` on the merge commit, push the tag, create a prerelease. Update the README install line on **both** branches with `--version 17.0.0-alpha.1` (Forms Audit Trail rule).

---

## Later (separate plans)

- **v0.2, "Hygiene and evidence":** F9 Archive history tab (added to `xss.spec.ts`, T6); F10 audit list, audit CSV and the access-review export, with the AU-08 visibility rule, `Exported` audit rows and the AU-12 CSV rules; F11 inactivity policy (`IDistributedBackgroundJob`, options validation on start) with the AU-06 hard rules, `MaxUsersPerRun`, keys-only dry-run logs and `Audit:RetentionDays` purge writing `AuditPurged` (AU-09); per-user name-snapshot erasure (AU-09); F12 scheduled reconciliation reusing `ReconcileAsync`; F13 `Umbraco.Community.ArchivedUsers.Automate` sub-package (User archived and unarchived triggers, Archive user and Disable user actions) with every AU-05 control in design 7.5, preceded by spike S7. **Gate:** the v0.2 plan is not written until U2 is closed; if U2 shows authoring needs no section beyond Automate, the AU-05 controls are mandatory tasks or the actions are dropped (else AU-05 is Critical). Tests the v0.2 plan must carry: T17 (AU-08), T18 (AU-12 CSV: `=`, `+`, `-`, `@`, tab, CR, LF, `|` in names and reasons, including after leading spaces), T19 (AU-06), T20 (AU-05, `Umbraco.Automate.Testing`). Open for Pascal before that plan: `RequireAdminAccess` on the two export endpoints (design "Security review changes", Open for Pascal 1).
- **v1.0, "Ready for clients":** F14 Inactive user trigger and Archive inactive users action (`days >= 30`, per-run cap, AU-05); stable `18.0.0` and `17.0.0`; Marketplace listing with screenshots; `security-analyst` review, which needs U1, U2 and U3 closed and a this-session dependency audit of the built packages (T21); full accessibility pass; SQL Server and load-balanced test pass, including T16 across two app instances on one SQL Server DB and the AU-03 archive-versus-enable race across nodes.

## Spec gaps found while planning, and how this plan resolves them

1. **Floors:** resolved. Section 10 and "Resolved by Pascal" now both say `[18.3.0,19.0.0)` / `[17.8.0,18.0.0)` (2026-10-30, AU-02). Global Constraints carries them.
2. **Unarchive source:** `UnarchiveAsync` in 7.2 has no `source`, but the audit row needs one. Added an `ArchiveSource source` parameter. The API derives `Manual` (one id) or `Bulk` (more), for archive too, so the request contract doesn't grow.
3. **Cancel message:** F1 says the user sees the cancel message, but per-user results are `{ id, status }`. Added an optional `message`.
4. **`ArchivedUserResponseModel` shape:** "core `UserResponseModel` fields plus" would mean copying every core field and drifting on each minor. The plan nests the core model as `user`. The model is pre-release, so this is still free to change.
5. **Explicit `enable: true` vs `wasDisabledBefore`:** superseded by AU-11 (design F5). `enable` can only narrow the site policy: `true` with the setting off is a 400, and nothing enables a user whose `wasDisabledBefore` is true.
6. **"Cap reason at 500":** read as "reject with 400", which matches the API table's validation rule, rather than silently truncating.
7. **Demo site:** Forms Audit Trail has none. This plan adds `demo/` with a seeder, because S1, E2E and the backoffice exercise need one.
8. **Spikes on 17:** the spec runs every spike on both lines in M0. This plan runs S1 on 17.8 early (Task 2 Step 5) to de-risk the port, and re-runs S2, S4, S5, U3 and the AU-01 tripwires at the start of Task 19.
9. **Marketplace category:** the spec's "backoffice extension" isn't a valid category; uses "Editor Tools", the value Forms Audit Trail had to fix to.

## Security review coverage

Every applied finding and every test the review requires (`security-review.md` section 5), with the task that pins it. "v0.2" and "v1.0" items are carried in "Later".

| Item | What | Task |
|---|---|---|
| T1 | Non-admin on admin, admin on superuser, on every write route; `IsApproved` unchanged; disabled performer refused (P9) | 7 (unit and integration), 8, 9, 11 (API, incl. mixed bulk), 16 `session` (e) |
| T2 | `DisableAsync`/`EnableAsync` don't check admin (tripwire) | 3, re-run on 17 in 19 |
| T3 | Record for an enabled user: filter shows it, view flags it, reconciliation removes it with `Reconcile` | 9, 10, 11, 12 |
| T4 | After archive: refresh rejected, authorize flow refuses, stamp changed; at floor and latest | 7 (stamp), 16 `session.spec.ts`, 17 nightly matrix |
| T5 | Archived API user gets no client-credentials token | 11 |
| T6 | Hostile names and reasons render as text in every surface | 16 `xss.spec.ts` |
| T7 | `build/check-xss.sh`: `htmlString` banned outright, non-literal `.term(` keys, `unsafeHTML`, `innerHTML`, `createContextualFragment`, `eval(` and more (P11) | 13 Step 6, 15 (name-cell render test) |
| T8 | Filter fails open | 12 |
| T9 | `userKinds` honoured; non-GET untouched | 12 (or 12-B) |
| T10 | `enable` can't widen policy; `reEnabled` audited | 9, 11 |
| T11 | Core Enable by an admin records that admin as performer | 10 |
| T12 | Body fields `source`, `performerKey`, `archivedUtc` ignored; form post 415 | 11 |
| T13 | Unknown and admin keys both `NotPermitted` to a non-admin | 7, 9, 11 |
| T14 | 401 / 403 on every route; reflection convention test | 11 |
| T15 | Non-admin `/filter/user` still hides admins and the superuser | 12 |
| T16 | Concurrent double archive: one record, one row, correct `WasDisabledBefore`; two instances on one SQL Server DB | 5, 7 (`ArchiveUserLocks`, real-DB concurrency test on SQLite and SQL Server nightly), 11 (parallel POSTs); v1.0 LB pass |
| T17 to T20 | Audit/export visibility, CSV, job hard rules, Automate controls | v0.2 plan (Later) |
| T21 | Parsed vulnerable-package output; osv-scanner green | 1 Step 4 (repo-own PR checks), 17 (publish) |
| U1 | Token validation mode and lifetime | 4a Step 1, 16 `session` (d), 17 README |
| U2 | Automate authoring rights and author in context | 4a Step 2; gates v0.2 |
| U3 | Does disable rotate the stamp | 3 Step 3 |
| U4 | Auto-link re-approval and names from claims | 4a Step 3, 10 (conditional test) |
| U5 | Surfaces that call `/filter/user` | 2 Step 6, 16 `pickers.spec.ts`, 17 README |
| AU-12 | Parameterised SQL, enum `orderBy`, keys-only logs | 5, 7, Global Constraints |
| AU-13 | 20,000 ceiling, debounced search | 12, 15 |
| AU-14b | Concurrent-safe migration, cluster-safe reconciliation, identical settings documented | 5, 10, 17 |
| AU-15 | Vite external, lockfiles (locked restore at the floor, P1), SHA pins, Dependabot, Trusted Publishing, repo-own gitleaks and osv-scanner, public-repo protections | 1, 3, 17 |

## Plan review changes (2026-10-30)

Source: the independent plan review (`qa-reviewer`, 2026-10-30, verdict ready-with-fixes, P1 to P15; kept in Pascal's private workspace) and Pascal's decisions of the same day. Spec changes are listed in the design's own "Plan review changes (2026-10-30)" section.

**Pascal's decisions applied**
- **Public repo at scaffold (GitHub Free).** Task 1 Step 12 makes the repo public after a gitleaks scan (Step 11) and switches on secret scanning with push protection, Dependabot alerts, CodeQL default setup and a ruleset on `main` and `v17`. From then on work goes through `feat/v0.1` and PRs.
- **PR checks in the repo's own CI from Task 1.** `build.yml` jobs `secrets` (gitleaks CLI, checksum-pinned), `deps` (osv-scanner and `build/check-vulnerable.sh`) and `build`. The mf-harness enrolment step and the Semgrep expectation are gone.
- **Public docs.** Only `design.md` and `plan.md` are copied into `docs/` (plus the generated `spikes.md`). `security-review.md`, `plan-review.md` and milestone review reports stay private. Review C checks this.
- **Hybrid build.** A "Milestone reviews" section sets the rules. Review A follows M0 (`qa-reviewer` plus Pascal's spike review). Review B follows Tasks 5 to 12 or 12-B (`qa-reviewer` and `security-analyst` in parallel). Review C comes before each release (`qa-reviewer` whole-branch, and a `v17` one in Task 19).

**Findings**

| ID | Applied? | Where |
|---|---|---|
| P1 | Applied. Locked restore only on the floor leg; latest legs pass `UmbracoTestVersion` / `UmbracoDemoVersion` and restore with `--force-evaluate -p:RestoreLockedMode=false`, never committing the lock files they produce | Global Constraints; Tasks 1, 2, 3 Step 7, 17 Step 3, 19 |
| P2 | Applied. `Harness_authenticates_backoffice_user_over_http`; S0 GO needs both harness tests; the fallback is now `WebApplicationFactory<Program>` over the demo site with per-fixture SQLite, and what it can't cover is listed | Task 3 Steps 1 and 2 |
| P3 | Applied. Gating, Tripwire and Recording categories; gating tests never edited to pass; each names its fallback; deferral fallback `UnarchiveInProgress` | Global Constraints; Task 3; Task 9 Step 4; Task 10 Step 3; design 7.2 |
| P4 | Applied. `**Gate:**` and `**Result / Decision:**` lines in `spikes.md`; M0 exit check with greps at the end of Task 4a; S3 decision objective; S5 labelled as the S5 gate in Task 5, with SQL Server run in CI on Linux (Apple Silicon emulation noted) and M1 blocked until GO | Tasks 1 Step 2, 2, 4, 4a Step 5, 5 Step 5 |
| P5 | Applied. Task 12-B in full task format, including the knock-on list for Tasks 11, 12, 13, 16, 17 and 19, settings and README | Task 12-B; design 7.2, 7.4 |
| P6 | Applied, with a design change. The reviewer's "assert the winner's `WasDisabledBefore`" alone would only expose the race, so the service now holds a per-user `ArchiveUserLocks` lock; real-DB concurrency test on SQLite and SQL Server nightly with a skip guard; API double POST | Task 7, Task 9, Task 11; design 7.2, 9 |
| P7 | Applied. `userGroupIds`, name filter, `HideDisabledUsersInBackOffice` and page-boundary API tests; Playwright `pickers.spec.ts` for the group "Add users" picker and the Content user picker | Task 12 Step 2; Task 16 |
| P8 | Applied, one part adjusted. `uc2-bulk.spec.ts` as a non-admin with self, invited and an editor; the admin can't be selected in the UI (core hides admins from non-admins), so that case is in Task 11's mixed-bulk API test. uc1 assertions for self, invited, helper text, D6 text and the hidden picker button. Vitest for `formatSummary` and the condition glue (`toCanArchiveInput`) | Tasks 13, 14, 16 |
| P9 | Applied: a test **and** an accepted-risk line. New rule: a disabled or missing performer gets `NotPermitted` for every key. Tests: unit (Tasks 7, 9), `Archived_admin_cannot_then_archive_the_other_admin`, `session` (e), Task 18. The truly simultaneous mutual archive can't be made deterministic in a test, so it is an accepted residual in the README and design 11; the superuser stays the recovery account. Mixed-bulk API test and the invited integration test added | Tasks 7, 9, 11, 16, 17, 18; design 4, F1, 7.2, 11 |
| P10 | Applied. Scope-failure test and the retry test | Task 7 |
| P11 | Applied. `htmlString` banned outright, literal `.term(` keys, more sinks, all in `build/check-xss.sh` with three probes; hostile performer name and hostile email in `xss.spec`; stale tag covered by a Vitest render test of the name cell (a stale record can't be made through the UI); emoji/CJK/markup round-trip API test | Global Constraints; Tasks 13, 15, 16, 11; design 4 S3 |
| P12 | Applied. (a) Task 8 uses the repository's `GetKeysAsync`; (b) `UserListPaging.DropSet` named; (c) `formatSummary` covers all eight reasons; (d) `canArchive` takes admin and superuser inputs; (e) `Umbraco.Cms.Api.Management` referenced and checked in the nuspec; (f) `Api_user_obtains_client_credentials_token` contract test | Tasks 1, 3, 8, 12, 13, 14, 17 |
| P13 | Applied. (a) `UMBRACO_HAS_API_USERS` in `Directory.Build.props`; (b) Core fixes land on `main` first; (c) `wwwroot` drift step in `build.yml` from Task 1; (d) "0 failed" instead of fixed spec counts | Tasks 1, 16, 19 |
| P14 | Applied, with a small spec addition (`take` 0 to 1,000, `skip` ≥ 0). API tests for unknown `orderBy`, `take=0`, `take=1001`, negative `skip`, and both `archivedBy` shapes | Task 9, Task 11; design 7.2 API |
| P15 | Applied. Sub-bullets in Task 7 Step 3 and Task 12 Step 4; one checkbox per scenario in Task 18 Step 4 | Tasks 7, 12, 18 |

Nothing was rejected. P6, P9 and P14 needed small spec additions beyond the reviewer's wording; the rows above give the reasons.
