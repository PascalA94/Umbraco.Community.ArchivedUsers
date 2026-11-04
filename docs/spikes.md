# Spikes and security UNKNOWNs

Public file. Never record details of an undisclosed vulnerability here; those go to Pascal's private workspace.

## S0: Integration harness

**Gate:** 
**Result / Decision:** 

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
- 17.8.0 (scratch site outside the repo, same seed): identical results; the filter matched action `Filter`. Recorded for Task 19.
- Floor and latest are the same versions on 2026-11-03 (18.3.0 and 17.8.0 are the newest releases on NuGet), so the "latest" run is the floor run.
- Core assemblies the spike needed: `FilterUserFilterController`, `IUserPresentationFactory` (`Umbraco.Cms.Api.Management`), `PagedViewModel<T>` (`Umbraco.Cms.Api.Common`, transitive through `Api.Management`). The Task 1 references (`Umbraco.Cms.Web.Common`, `Umbraco.Cms.Api.Management`) cover them (P12e).

## S2: Does `EnableAsync` publish `UserSavedNotification`, deferred to an outer scope?

**Gate:** 
**Result / Decision:** 

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

**Gate:** 
**Result / Decision:** 

## S5: Package migration on SQLite and SQL Server

**Gate:** pending (Task 5 Step 5)
**Result / Decision:** 

## S6: Frontend wiring and the OpenAPI client

**Gate:** 
**Result / Decision:** 

## U1: Access-token validation mode and lifetime

Pass (closed) when: The mode and the access-token lifetime are recorded with the source line, and T4's observed result matches.

**Result / Decision:** 

## U2: Automate authoring rights, and whether the action context exposes the authoring user

Pass (closed) when: The permission needed to author, and whether the author is available to an action, are recorded with sources.

**Result / Decision:** 

## U3: Does `DisableAsync` rotate the security stamp?

Pass (closed) when: The answer is recorded and the package's own rotation test passes either way.

**Result / Decision:** 

## U4: Does external-login auto-link re-approve a disabled user, or update names from IdP claims?

Pass (closed) when: Both answers recorded with the source line.

**Result / Decision:** 

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
