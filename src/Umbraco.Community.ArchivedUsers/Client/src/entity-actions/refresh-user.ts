import { ARCHIVED_USERS_ARCHIVE_STATUS_CONTEXT } from '../archive-status/archive-status.context-token.js';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbContextConsumerController } from '@umbraco-cms/backoffice/context-api';
import { UMB_USER_DETAIL_STORE_CONTEXT } from '@umbraco-cms/backoffice/user';
import { UserStateModel } from '@umbraco-cms/backoffice/external/backend-api';

/**
 * After an archive or unarchive: refresh the archive status (re-evaluates the Archive/Unarchive conditions) and,
 * for archive, update core's user store so the open editor shows Disabled, as core's own Disable does (docs/spikes.md S6).
 */
export async function refreshUsers(host: UmbControllerHost, uniques: string[], disabled: boolean): Promise<void> {
  const status = await new UmbContextConsumerController(host, ARCHIVED_USERS_ARCHIVE_STATUS_CONTEXT).asPromise().catch(() => undefined);
  const store = await new UmbContextConsumerController(host, UMB_USER_DETAIL_STORE_CONTEXT).asPromise().catch(() => undefined);
  for (const unique of uniques) {
    await status?.refresh(unique);
    if (disabled) store?.updateItem(unique, { state: UserStateModel.DISABLED });
  }
}
