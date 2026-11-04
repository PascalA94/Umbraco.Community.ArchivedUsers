import { ARCHIVED_USERS_ARCHIVE_STATUS_CONTEXT } from '../archive-status/archive-status.context-token.js';
import { canArchive } from './predicates.js';
import { toCanArchiveInput } from './condition-inputs.js';
import { UmbConditionBase } from '@umbraco-cms/backoffice/extension-registry';
import type { UmbConditionConfigBase, UmbConditionControllerArguments } from '@umbraco-cms/backoffice/extension-api';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UMB_USER_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/user';
import { UMB_CURRENT_USER_CONTEXT } from '@umbraco-cms/backoffice/current-user';
import { observeMultiple } from '@umbraco-cms/backoffice/observable-api';

/**
 * Shows Archive when the open user may be archived. It only hides the action; the server authorises every user
 * itself (AU-01, P12d). Self-contained because core's user condition base isn't exported (design 7.3).
 */
export class ArchivedUsersCanArchiveCondition extends UmbConditionBase<UmbConditionConfigBase> {
  constructor(host: UmbControllerHost, args: UmbConditionControllerArguments<UmbConditionConfigBase>) {
    super(host, args);
    this.permitted = false;

    this.consumeContext(UMB_CURRENT_USER_CONTEXT, (current) => {
      this.consumeContext(UMB_USER_WORKSPACE_CONTEXT, (workspace) => {
        this.consumeContext(ARCHIVED_USERS_ARCHIVE_STATUS_CONTEXT, async (status) => {
          if (!current || !workspace || !status) {
            this.permitted = false;
            return;
          }

          const config = await status.config();
          if (!config) {
            this.permitted = false;
            return;
          }

          this.observe(
            observeMultiple([current.unique, current.isAdmin, workspace.unique, workspace.state, workspace.userGroupUniques]),
            ([currentUnique, isAdmin, unique, state, groups]) => {
              if (!unique || !currentUnique) {
                this.permitted = false;
                return;
              }

              this.observe(
                observeMultiple([status.observeIsLoaded(unique), status.observeIsArchived(unique), status.observeIsStale(unique)]),
                ([loaded, isArchived, isStale]) => {
                  this.permitted =
                    loaded &&
                    canArchive(
                      toCanArchiveInput(
                        { unique, state, userGroupUniques: groups.map((g) => g.unique) },
                        { unique: currentUnique, isAdmin: isAdmin ?? false },
                        { isArchived, isStale },
                        config.adminGroupId,
                        config.superUserId,
                      ),
                    );
                },
                '_archivedUsersStatus',
              );
            },
            '_archivedUsersUser',
          );
        });
      });
    });
  }
}

export { ArchivedUsersCanArchiveCondition as api };
