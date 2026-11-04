# Spikes and security UNKNOWNs

Public file. Never record details of an undisclosed vulnerability here; those go to Pascal's private workspace.

## S0: Integration harness

**Gate:** GO
**Result / Decision:** `HarnessServiceTests.Harness_boots_and_resolves_IUserService` and `HarnessTests.Harness_authenticates_backoffice_user_over_http` pass on SQLite (18.3.0). No fallback needed.

- Base classes: service tests inherit `UmbracoIntegrationTest` with `[UmbracoTest(Database = NewSchemaPerTest)]`; HTTP tests inherit the package's `ApiTestBase : UmbracoTestServerTestBase` (also `NewSchemaPerTest`, `Boot = true`). `ManagementApiTest<T>` exists publicly but caches tokens in a static and needs a controller type parameter, so the package copies its auth flow instead.
- `CreateAuthenticatedClientAsync(IUser)`: sets the password through a reset token (`ICoreBackOfficeUserManager.GeneratePasswordResetTokenAsync` + `IUserService.ChangePasswordAsync`), `POST security/back-office/login` with a cookie-keeping `HttpClient`, `GET security/back-office/authorize` (client `umbraco-back-office`, PKCE S256, redirect = host + `Umbraco:CMS:Security:AuthorizeCallbackPathName`), then `POST /umbraco/management/api/v1/security/back-office/token` with `grant_type=authorization_code`. The access token arrives in the `__Host-umbAccessToken` cookie; requests send it as a cookie plus a placeholder `Authorization: Bearer [redacted]` header, as the backoffice does. Nothing is faked: `CustomTestAuthSetup` is overridden to install no test auth handler.
- **Back-office token endpoint:** `/umbraco/management/api/v1/security/back-office/token` (Task 16 `session.spec.ts`).
- Package migration runner for `MigrationTests`: `PackageMigrationRunner.RunPackagePlansAsync(IEnumerable<string>)` (public class in `Umbraco.Infrastructure.Install`), with `IMigrationPlanExecutor` as the lower-level option.
- Wiring notes (needed because the framework's own `[SetUpFixture]` and deps file live in its assembly): a global-namespace `[SetUpFixture]` that runs `GlobalSetupTeardown`, a project `appsettings.Tests.json` (SQLite), and a post-build copy of the test deps file to `Umbraco.Tests.Integration.deps.json` for `WebApplicationFactory`. NUnit is pinned to 3.14.0, the version `Umbraco.Cms.Tests` is built against.
- SQL Server: the same framework reads `Tests:Database:DatabaseType=SqlServer` and `Tests:Database:SQLServerMasterConnectionString` from environment variables (`Tests__Database__…`), which `nightly.yml` sets.

## S1: `/filter/user` action-filter hide at runtime

**Gate:** GO
**Result / Decision:** Option A (server action filter on `/filter/user`) works on 18.3.0 and 17.8.0. Build Task 12, not 12-B. Source: [FilterUserFilterController.cs at release-18.3.0](https://github.com/umbraco/Umbraco-CMS/blob/ee1861c1dfd925d8fdd476be2bbd48b7743ad737/src/Umbraco.Cms.Api.Management/Controllers/User/Filter/FilterUserFilterController.cs) and [at release-17.8.0](https://github.com/umbraco/Umbraco-CMS/blob/83984e2515634f5812d9af99e0149cd7d94622c8/src/Umbraco.Cms.Api.Management/Controllers/User/Filter/FilterUserFilterController.cs).

Core facts (18.3.0, commit `ee1861c`):
- Controller `FilterUserFilterController : UserFilterControllerBase`. Route attribute on the base: `[VersionedApiBackOfficeRoute("filter/user")]`, policy `SectionAccessUsers`. Attribute route template seen at runtime: `umbraco/management/api/v{version:apiVersion}/filter/user`.
- Live action `Filter2(CancellationToken cancellationToken, int skip = 0, int take = 100, UserOrder orderBy = UserOrder.UserName, Direction orderDirection = Direction.Ascending, [FromQuery] HashSet<Guid>? userGroupIds = null, [FromQuery] HashSet<UserState>? userStates = null, [FromQuery] HashSet<UserKind>? userKinds = null, string filter = "")`. `Filter` is `[NonAction]` and obsolete (removal in v20).
- It builds `new UserFilter { IncludedUserGroups = userGroupIds, IncludeUserStates = userStates, IncludeUserKinds = userKinds, NameFilters = string.IsNullOrEmpty(filter) ? null : { filter } }` and calls `IUserService.FilterAsync(CurrentUserKey(_backOfficeSecurityAccessor), userFilter, skip, take, orderBy, orderDirection)`; maps with the **sync** `IUserPresentationFactory.CreateResponseModel(IUser)`; returns `PagedViewModel<UserResponseModel>`. A failed attempt returns `UserOperationStatusResult(status)`.
- 17.8.0 (commit `83984e2`): live action is `Filter` with the same parameters minus `userKinds`, and the same body minus `IncludeUserKinds`. `UserKind` itself exists on 17.8 (`Umbraco.Core/Models/Membership/UserKind.cs`), so `UMBRACO_HAS_API_USERS` stays defined on `v17` (P13a).
- **`context.ActionArguments` only holds parameters the request supplied** (seen at runtime: `cancellationToken,skip,take` for a plain list load). The package filter must apply core's defaults for missing ones (`skip 0`, `take 100`, `orderBy UserName`, `orderDirection Ascending`, empty filter).

Runtime (demo site, 44 seeded users, 3 hidden by a throwaway filter matching `ControllerTypeInfo == typeof(FilterUserFilterController)`, `FilterAsync(..., 0, int.MaxValue, ...)`, in-memory paging):
- 18.3.0: API pages `skip=0,10,20,30,40&take=10` gave `total=41` on every page, 41 unique users, none of the 3 hidden. `filter=former` → 6 (2 hidden dropped). `userStates=Disabled` → 9. The backoffice Users collection (`filter/user?orderBy=Name&orderDirection=Ascending&skip=0&take=50`) rendered our payload with the hidden user missing and **no console errors**; search in the collection, the user group member list and the "Choose users" picker in a user group all called `/filter/user` and hid the users.
- 17.8.0 (scratch site outside the repo, same seed): identical results; the filter matched action `Filter`. Recorded for Task 19. This 17.8 run is manual and not reproducible from the tree; Task 19's `FilterUserTests` on the `v17` line are the repeatable proof.
- Still to prove at runtime: the Content User Picker property editor (Task 16 `pickers.spec.ts`, a hard gate for the README D1 table) and S1 on 18.4+ when it ships (the CI latest leg).
- Floor and latest are the same versions on 2026-11-03 (18.3.0 and 17.8.0 are the newest releases on NuGet), so the "latest" run is the floor run.
- Core assemblies the spike needed: `FilterUserFilterController`, `IUserPresentationFactory` (`Umbraco.Cms.Api.Management`), `PagedViewModel<T>` (`Umbraco.Cms.Api.Common`, transitive through `Api.Management`). The Task 1 references (`Umbraco.Cms.Web.Common`, `Umbraco.Cms.Api.Management`) cover them (P12e).

## S2: Does `EnableAsync` publish `UserSavedNotification`, deferred to an outer scope?

**Gate:** GO
**Result / Decision:** `CoreContractTests.EnableAsync_publishes_UserSavedNotification` and `CoreContractTests.EnableAsync_inside_outer_scope_defers_UserSaved_until_outer_complete` pass (Gating) on 18.3.0. Task 10 uses `UserSavedNotification` (no `CoreEnableActionFilter`); Task 9 relies on the deferral (no `UnarchiveInProgress` marker).

## S3: Which endpoints each surface calls; `FilterAsync` with `HideDisabledUsersInBackOffice`

**Result / Decision:** Task 8 uses **paged IUserService reads** (`IUserService.GetAll(pageIndex, pageSize, out total, orderBy, orderDirection, userState: [Disabled])` plus `UserVisibility.CanSee`), because `FilterAsync` with `IncludeUserStates = { Disabled }` returns no users when `HideDisabledUsersInBackOffice` is on. Source: [UserService.CreateBaseUserFilter at release-18.3.0](https://github.com/umbraco/Umbraco-CMS/blob/ee1861c1dfd925d8fdd476be2bbd48b7743ad737/src/Umbraco.Core/Services/UserService.cs#L1461-L1484).

- Runtime, 18.3.0 demo with `Umbraco:CMS:Security:HideDisabledUsersInBackOffice: true`: `filter/user?userStates=Disabled` → `total=0`; `filter/user` → 34 of 44 (the 10 disabled users gone; the invited user stays).
- Why: `CreateBaseUserFilter` sets `filter.IncludeUserStates = { Active, Invited, LockedOut, Inactive }` when the setting is on, so the base filter excludes Disabled whatever the caller asks for. The legacy paged `GetAll(... UserState[]? userState ...)` overload does not apply the setting.
- Note: `filter/user?userStates=Disabled` also returns the **invited** user (core treats not-approved as Disabled in the query). The archive service already skips Invited users, so clean-up must count after that skip, not before.
- Endpoints per surface (DevTools Network, 18.3.0):
  - Users collection (grid and table): `GET filter/user?orderBy=Name&orderDirection=Ascending&skip=0&take=50`; search adds `filter=`.
  - User editor: `GET user/{id}`, `user/{id}/calculate-start-nodes`, `user/{id}/client-credentials`, `user/configuration` (never filtered).
  - User group editor, member list: `GET filter/user?userGroupIds={groupId}&take=100`, then `GET item/user?id=…` for the member refs.
  - User group "Choose" users picker: `GET filter/user?orderBy=Name&orderDirection=Ascending&skip=0&take=50` (core's `Umb.Modal.CollectionItemPicker` over `Umb.Collection.User`).
  - Current user: `GET user/current` (never filtered).

## S4: Core user-service behaviour, authorisation, security stamp and audit calls

**Gate:** GO
**Result / Decision:** All Gating tests pass: `CoreContractTests.AuthorizeAccessAsync_refuses_non_admin_on_admin`, `CoreContractTests.UpdateSecurityStampAsync_changes_stamp_without_HttpContext`. Tripwires pass: `DisableAsync_does_not_check_admin`, `EnableAsync_does_not_check_admin`, `FilterAsync_hides_admins_from_non_admin`. Recording tests pass as written.

Notes for Task 7:
- **Per-user status map** (`IUserService.DisableAsync`, one key per call): already disabled → `Success`; self → `CannotDisableSelf`; invited → `CannotDisableInvitedUser`; `SuperUserKey` performer → `Success` for editor, admin and user manager; non-admin performer on an admin → `Success` (core does not authorise, AU-01).
- **Authorisation call:** `IUserPermissionService.AuthorizeAccessAsync(IUser performer, IEnumerable<Guid> userKeys)` → `UserAuthorizationStatus` (`Success`, or `UnauthorizedMissingAdminAccess`). Implementation at 18.3.0: an admin performer always gets `Success`; otherwise any admin target is refused ([UserPermissionService.cs](https://github.com/umbraco/Umbraco-CMS/blob/ee1861c1dfd925d8fdd476be2bbd48b7743ad737/src/Umbraco.Core/Services/UserPermissionService.cs)). It has no superuser rule, which `UserVisibility` adds.
- **Stamp rotation route:** resolve `IBackOfficeUserManager` in a DI scope, `FindByIdAsync(userKey.ToString())` (the store accepts a key string), then `UpdateSecurityStampAsync` from `UserManager<BackOfficeIdentityUser>` (the registered implementation, `BackOfficeUserManager`, derives from it; the interface does not declare the method). Works with no `HttpContext`.
- **Core audit call:** `IAuditService.AddAsync(AuditType.Custom, performerKey, target.Id, "User", "ArchivedUsers: <Action> (<Source>)")`, read back through `GetItemsByEntityAsync(target.Id, ...)` (`AuditService_write_is_readable_back`). `AddAsync` needs a real user key as performer; for system actions the package uses `SuperUserKey` and says so in the comment.
- **Client credentials (T5):** `IBackOfficeUserClientCredentialsManager.SaveAsync(userKey, clientId, secret)` (scoped; prefixes the id with `umbraco-back-office-`); token request `POST /umbraco/management/api/v1/security/back-office/token` with `grant_type=client_credentials`, `client_id`, `client_secret` → 200 with `access_token` (`CoreContractApiTests.Api_user_obtains_client_credentials_token`). Core's `FindUserAsync(clientId)` returns null for a user whose `IsApproved` is false, which is what T5 relies on.

## S5: Package migration on SQLite and SQL Server

**Gate:** GO
**Result / Decision:** The migration (`AsyncMigrationBase`, one step `AddArchivedUserTables`, state `archived-users-1`) works unchanged on SQLite and SQL Server 2022: `MigrationTests.Plan_creates_both_tables`, `MigrationTests.Plan_is_safe_to_rerun`, `MigrationTests.Step_tolerates_table_created_concurrently` and every `ArchivedUserRepositoryTests` test pass on both. No per-provider branch needed.

- SQLite: `dotnet test src/Umbraco.Community.ArchivedUsers.IntegrationTests --filter "FullyQualifiedName~MigrationTests|FullyQualifiedName~ArchivedUserRepositoryTests"` → 12/12 passed (2026-11-04).
- SQL Server: run locally against `mcr.microsoft.com/mssql/server:2022-latest` (linux/amd64 under emulation on Apple Silicon) with `Tests__Database__DatabaseType=SqlServer` and `Tests__Database__SQLServerMasterConnectionString`; the framework created and dropped `UmbracoTests-*` databases (seen in the SQL Server log). Whole integration suite → 28/28 passed. `.github/workflows/nightly.yml` (`integration-sqlserver`) repeats this in CI once the repo is pushed; **no CI run URL exists yet**, because nothing has been pushed (Pascal's to-do).
- Demo site on SQL Server: an empty `AuDemo` database, unattended install, started twice. Both tables exist, and `umbracoKeyValue` has `Umbraco.Core.Upgrader.State+Umbraco.Community.ArchivedUsers = archived-users-1`. The second start logged no Error or Fatal entries.
- Found on the way: Umbraco runs package migrations unattended at boot (`Umbraco:CMS:Unattended:PackageMigrationsUnattended`, default true). Two SQLite writers creating the same table in parallel deadlock (seen in a stack dump: the boot upgrader and a test both in `AddArchivedUserTables.CreateIfMissing`). The test project's `appsettings.Tests.json` turns unattended package migrations off, so tests decide when the plan runs. Sites are not affected: one SQLite site runs one upgrader.
- `ColumnInfo.IsNullable` from `GetColumnsInSchema` did not report `reEnabled` as NOT NULL on SQLite, so the test checks the behaviour instead: inserting NULL into `reEnabled` fails, and an omitted value defaults to 0.

## S6: Frontend wiring and the OpenAPI client

**Gate:** GO
**Result / Decision:** Every item works as the spec says or has a named replacement (below). Checked at runtime on the 18.3.0 demo with throwaway manifests (reverted), no console errors, and against the `@umbraco-cms/backoffice@18.3.0` sources ([user package at release-18.3.0](https://github.com/umbraco/Umbraco-CMS/tree/ee1861c1dfd925d8fdd476be2bbd48b7743ad737/src/Umbraco.Web.UI.Client/src/packages/user/user)).

| Item | Result |
|---|---|
| `entityAction` (kind `default`) for `user` | Works as spec: shows in the user editor's actions menu and receives the user's `unique`. |
| `entityBulkAction` on `Umb.Collection.User` (`UMB_COLLECTION_ALIAS_CONDITION`) | Works as spec: shows in core's selection bar and receives `selection`. |
| `collectionAction` kind `button` with an `api` extending `UmbCollectionActionBase`, plus `UMB_IN_MODAL_CONDITION_ALIAS` `match: false` | Works as spec: shows next to Create; absent in the user group "Choose" users picker modal (count 0). Same conditions as core's `Umb.CollectionAction.User.Create`. |
| `menuItem` in `Umb.Menu.UserManagement`, weight 190, `entityType` + `workspace` kind `default` | Works as spec: sits between Users (200) and User Groups; opens `/section/user-management/workspace/<entityType>`. `UMB_USER_MANAGEMENT_MENU_ALIAS` is **not** exported from a public entry point, so the manifest uses the literal `'Umb.Menu.UserManagement'`. |
| `collectionView` kind `table` | **Replacement:** the kind exists (`Umb.Kind.CollectionView.Table`, `meta.columns: [{ field, label, valueType }]`) but has no sortable headers and no custom cell elements. The Archived view needs sortable name and archived columns and the stale tag beside the name, so it is a custom `collectionView` element built on `umb-table` (`archived-user-table.element.ts`), with the `archived-user-name-cell` element. |
| Refresh after an action | Core's Disable updates `UMB_USER_DETAIL_STORE_CONTEXT.updateItem(id, { state })` (the editor observes the store) and, for bulk, dispatches `UmbRequestReloadChildrenOfEntityEvent({ entityType, unique })` from `UMB_ENTITY_CONTEXT` through `UMB_ACTION_EVENT_CONTEXT`. The package does the same: store update for the editor, reload event for collections. Both tokens are exported (`@umbraco-cms/backoffice/user`, `/action`, `/entity`, `/entity-action`). |
| How `peek` renders `data.message` (AU-04) | As a **Lit text binding** (`notification-layout-default.element.ts`: `return this.data.message`). Only `data.htmlMessage` goes through `unsafeHTML(sanitizeHTML(...))`. The package uses `message` only and never `htmlMessage`. Core's own Disable action passes the user name through `localize.term` into `umbConfirmModal`'s headline, which is the pattern AU-04 bans. |
| Condition re-evaluation | `UmbConditionBase` calls the extension manager's `onChange` whenever `permitted` is set to a new value (`core/extension-registry/conditions/condition-base.controller.js`), so a condition that observes the archive-status context re-evaluates after an action. |
| Admin group and superuser uniques | **Replacement:** not exposed by the client (`UMB_CURRENT_USER_CONTEXT.isAdmin` exists for the current user only). `GET archived-users/config` also returns `adminGroupId` and `superUserId` (`Constants.Security.AdminGroupKey`, `Constants.Security.SuperUserKey`). |
| OpenAPI (Step 5) | Works as spec. Register with `builder.AddBackOfficeOpenApiDocument("archived-users", d => d.WithTitle("Archived Users").WithBackOfficeAuthentication().WithJsonOptions(Constants.JsonOptionsNames.BackOffice))` (`Umbraco.Cms.Api.Common.OpenApi`) and put `[MapToApi("archived-users")]` on the controller base (it replaces core's `[MapToApi("management")]`; `AllowMultiple = false`). Served at **`/umbraco/openapi/archived-users.json`**; absent from `management.json`. Operation ids come from Umbraco's `UmbracoOperationIdTransformer` (method + route, e.g. `GetSpikeArchivedUsersConfig`). `@hey-api/openapi-ts` 0.99.0 (already in the tree through `@umbraco-cms/backoffice`) generates `sdk.gen.ts` with `security: [{ scheme: 'bearer', type: 'http' }]`; calls pass `client: umbHttpClient` from `@umbraco-cms/backoffice/http-client` and are wrapped in `tryExecute` from `@umbraco-cms/backoffice/resources`. No fallback needed. |

## U1: Access-token validation mode and lifetime

Pass (closed) when: The mode and the access-token lifetime are recorded with the source line, and T4's observed result matches.

**Result / Decision:** Access tokens are **reference tokens validated against the token store**, not self-contained, on both floors ([UmbracoBuilderAuthExtensions.cs at 18.3.0](https://github.com/umbraco/Umbraco-CMS/blob/ee1861c1dfd925d8fdd476be2bbd48b7743ad737/src/Umbraco.Cms.Api.Common/DependencyInjection/UmbracoBuilderAuthExtensions.cs)). Lifetime: access token = `Umbraco:CMS:Global:TimeOut` / 4 (**5 minutes** with the default `TimeOut` of 20 minutes); refresh token = `TimeOut`.

- Server: `options.UseReferenceAccessTokens().UseReferenceRefreshTokens()` and `options.SetAccessTokenLifetime(new TimeSpan(timeOut.Ticks / 4)); options.SetRefreshTokenLifetime(timeOut);` ([UmbracoBuilderAuthExtensions.cs L85-L98 at 18.3.0](https://github.com/umbraco/Umbraco-CMS/blob/ee1861c1dfd925d8fdd476be2bbd48b7743ad737/src/Umbraco.Cms.Api.Common/DependencyInjection/UmbracoBuilderAuthExtensions.cs#L85-L98); identical lines [at 17.8.0](https://github.com/umbraco/Umbraco-CMS/blob/83984e2515634f5812d9af99e0149cd7d94622c8/src/Umbraco.Cms.Api.Common/DependencyInjection/UmbracoBuilderAuthExtensions.cs#L85-L98)).
- Validation: `options.UseLocalServer(); ... options.EnableTokenEntryValidation();` ([L157-L167](https://github.com/umbraco/Umbraco-CMS/blob/ee1861c1dfd925d8fdd476be2bbd48b7743ad737/src/Umbraco.Cms.Api.Common/DependencyInjection/UmbracoBuilderAuthExtensions.cs#L157-L167)), so every API call checks the token entry in the database. A revoked token stops working on the next request.
- Revocation on disable: `RevokeUserAuthenticationTokensNotificationHandler` handles `UserSavedNotification` and revokes tokens when `user.IsLockedOut || user.IsApproved is false` ([RevokeUserAuthenticationTokensNotificationHandler.cs at 18.3.0](https://github.com/umbraco/Umbraco-CMS/blob/ee1861c1dfd925d8fdd476be2bbd48b7743ad737/src/Umbraco.Cms.Api.Management/Handlers/RevokeUserAuthenticationTokensNotificationHandler.cs)).
- **T4 runtime confirmation is pending (Task 16 `session.spec.ts` (d)).** Until it passes, U1 is closed on source reading only, and the Task 17 README wording waits on it.
- Consequence: no README residual for in-flight tokens is needed if T4 (Task 16 `session.spec.ts` (d)) confirms an archived user's last access token is refused. If T4 shows otherwise, the README states the 5-minute window.

## U2: Automate authoring rights, and whether the action context exposes the authoring user

Pass (closed) when: The permission needed to author, and whether the author is available to an action, are recorded with sources.

**Result / Decision:** Authoring needs **no section beyond Automate**: access to the Automate section (`[Authorize(Policy = AutomateAuthorizationPolicies.SectionAccessAutomate)]` on `UmbracoAutomateManagementControllerBase`) plus membership of a user group assigned to the automation's workspace (`AutomateAuthorizationPolicies.WorkspaceAccess`; admins bypass it). Same on the v17 and v18 lines. Sources: [umbraco/Umbraco.Automate, `src/Umbraco.Automate.Web/Api/Management/Common/Controllers/UmbracoAutomateManagementControllerBase.cs`](https://github.com/umbraco/Umbraco.Automate) and the [Workspaces docs](https://docs.umbraco.com/umbraco-automate/17.latest/concepts/workspaces) ("can view, edit, and run the automations in the workspace").

The action execution context does **not** expose the authoring user. `ActionContext.ExecutionContext` is an `AutomationExecutionContext` with `ServiceAccountKey` (the workspace's API user the run executes as), `InitiatorType` (`user`, `scheduled`, `webhook`, `ai-agent`), an optional `InitiatorId` string, and `WorkspaceId`, `AutomationId`, `AutomationName`, `RunId` (`src/Umbraco.Automate.Core/Execution/AutomationExecutionContext.cs`).

**AU-05 controls (design 7.5) are mandatory in the v0.2 plan; without them AU-05 is Critical.** Spike S7 re-checks both answers on the Automate versions v0.2 targets (not verified here: what `InitiatorId` holds for each initiator type, and a per-release diff of the context type).

## U3: Does `DisableAsync` rotate the security stamp?

Pass (closed) when: The answer is recorded and the package's own rotation test passes either way.

**Result / Decision:** Core's `DisableAsync` does **not** rotate the security stamp on 18.3.0 (`CoreContractTests.DisableAsync_does_not_rotate_security_stamp` observes the same stamp before and after). The package rotates it itself after every disable (AU-02, Task 7). U3 closed.

## U4: Does external-login auto-link re-approve a disabled user, or update names from IdP claims?

Pass (closed) when: Both answers recorded with the source line.

**Result / Decision:** (a) **No.** Core's auto-link never sets `IsApproved`: for an existing user it calls the site's `OnAutoLinking` callback, then `OnExternalLogin`, then `AddLoginAsync` and signs in ([BackOfficeSignInManager.cs L192-L240 at 18.3.0](https://github.com/umbraco/Umbraco-CMS/blob/ee1861c1dfd925d8fdd476be2bbd48b7743ad737/src/Umbraco.Cms.Api.Management/Security/BackOfficeSignInManager.cs#L192-L240)). A site callback **could** set `IsApproved = true`; the save then goes through the user store and `IUserService`, so `UserSavedNotification` fires and F7 removes the archive record with a `CoreEnable` row (S2 shows `UserSaved` fires on enable). No conditional contract test is added to Task 10, because core itself never re-approves.
(b) **Names can come from the IdP.** A new auto-linked user is created with `loginInfo.Principal.Identity.Name` ([L243-L252](https://github.com/umbraco/Umbraco-CMS/blob/ee1861c1dfd925d8fdd476be2bbd48b7743ad737/src/Umbraco.Cms.Api.Management/Security/BackOfficeSignInManager.cs#L243-L252)), and `OnAutoLinking` callbacks commonly copy claims onto existing users. User names are therefore attacker-influenced text, which confirms AU-04 (render as text only). No design change.

## U5: Which non-Users-section surfaces call `/filter/user`?

Pass (closed) when: Every surface that calls `/filter/user` is listed.

**Result / Decision:** Every surface found that lists users goes through core's user collection repository, which calls `GET filter/user` ([user-collection.server.data-source.ts at release-18.3.0](https://github.com/umbraco/Umbraco-CMS/blob/ee1861c1dfd925d8fdd476be2bbd48b7743ad737/src/Umbraco.Web.UI.Client/src/packages/user/user/collection/repository/user-collection.server.data-source.ts), `UserService.getFilterUser`). The user picker modal token is `Umb.Modal.CollectionItemPicker` over `Umb.Collection.User` ([user-picker-modal.token.ts](https://github.com/umbraco/Umbraco-CMS/blob/ee1861c1dfd925d8fdd476be2bbd48b7743ad737/src/Umbraco.Web.UI.Client/src/packages/user/user/modals/user-picker/user-picker-modal.token.ts)).

| Surface | Endpoint | Hides archived under Option A | Under Option B |
|---|---|---|---|
| Users collection | `filter/user` | Yes | Yes (swapped repository) |
| User group member list | `filter/user?userGroupIds=…` | **Yes: archived members drop out of the list** (still members) | Yes |
| User group "Choose" users picker | `filter/user` (collection item picker) | Yes | Yes |
| User Picker property editor in Content (`Umb.PropertyEditorUi.UserPicker` → `UMB_USER_PICKER_MODAL`) | `filter/user` (same modal, source-read; runtime check in Task 16 `pickers.spec.ts`) | Yes | Yes |
| User editor, `/item/user` refs (content "created by", group member refs), current user | `user/{id}`, `item/user`, `user/current` | No (never filtered) | No |
- `/filter/user` requires `SectionAccessUsers`, so an editor without Users-section access cannot use the user-picker modal in core either; the package changes nothing there.
- README D1 section (Task 17) carries this table.
