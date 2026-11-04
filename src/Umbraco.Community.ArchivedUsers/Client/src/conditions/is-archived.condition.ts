import { ARCHIVED_USERS_ARCHIVE_STATUS_CONTEXT } from '../archive-status/archive-status.context-token.js';
import { UmbConditionBase } from '@umbraco-cms/backoffice/extension-registry';
import type { UmbConditionConfigBase, UmbConditionControllerArguments } from '@umbraco-cms/backoffice/extension-api';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UMB_USER_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/user';

/** Shows Unarchive for any user with an archive record, stale or not (design 7.3). */
export class ArchivedUsersIsArchivedCondition extends UmbConditionBase<UmbConditionConfigBase> {
  constructor(host: UmbControllerHost, args: UmbConditionControllerArguments<UmbConditionConfigBase>) {
    super(host, args);
    this.permitted = false;

    this.consumeContext(UMB_USER_WORKSPACE_CONTEXT, (workspace) => {
      this.consumeContext(ARCHIVED_USERS_ARCHIVE_STATUS_CONTEXT, (status) => {
        if (!workspace || !status) {
          this.permitted = false;
          return;
        }

        this.observe(
          workspace.unique,
          (unique) => {
            if (!unique) {
              this.permitted = false;
              return;
            }

            this.observe(status.observeIsArchived(unique), (isArchived) => (this.permitted = isArchived), '_archivedUsersStatus');
          },
          '_archivedUsersUser',
        );
      });
    });
  }
}

export { ArchivedUsersIsArchivedCondition as api };
