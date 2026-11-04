# Spikes and security UNKNOWNs

Public file. Never record details of an undisclosed vulnerability here; those go to Pascal's private workspace.

## S0: Integration harness

**Gate:** 
**Result / Decision:** 

## S1: `/filter/user` action-filter hide at runtime

**Gate:** 
**Result / Decision:** 

## S2: Does `EnableAsync` publish `UserSavedNotification`, deferred to an outer scope?

**Gate:** 
**Result / Decision:** 

## S3: Which endpoints each surface calls; `FilterAsync` with `HideDisabledUsersInBackOffice`

**Result / Decision:** 

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

**Result / Decision:** 
