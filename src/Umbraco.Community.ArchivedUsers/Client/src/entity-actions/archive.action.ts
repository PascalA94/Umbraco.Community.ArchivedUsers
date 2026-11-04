import { archivedUsersApi } from '../api/archived-users.api.js';
import { ARCHIVED_USERS_ARCHIVE_STATUS_CONTEXT } from '../archive-status/archive-status.context-token.js';
import { showNeverLoggedInHint } from '../conditions/predicates.js';
import { ARCHIVED_USERS_ARCHIVE_CONFIRM_MODAL } from '../modal/archive-confirm.modal-token.js';
import { notifyResults } from '../results/notify.js';
import { refreshUsers } from './refresh-user.js';
import { UmbEntityActionBase } from '@umbraco-cms/backoffice/entity-action';
import { umbOpenModal } from '@umbraco-cms/backoffice/modal';
import { UMB_USER_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/user';

/** F1: archive the user open in the editor. */
export class ArchivedUsersArchiveEntityAction extends UmbEntityActionBase<never> {
  override async execute() {
    const unique = this.args.unique;
    if (!unique) throw new Error('No user to archive.');

    const status = await this.getContext(ARCHIVED_USERS_ARCHIVE_STATUS_CONTEXT);
    const config = await status?.config();
    const workspace = await this.getContext(UMB_USER_WORKSPACE_CONTEXT).catch(() => undefined);
    const user = workspace?.getData();

    const value = await umbOpenModal(this, ARCHIVED_USERS_ARCHIVE_CONFIRM_MODAL, {
      data: {
        mode: 'archive',
        count: 1,
        requireReason: config?.requireReason ?? false,
        showNeverLoggedInHint: user ? showNeverLoggedInHint({ state: user.state, lastLoginDate: user.lastLoginDate, isArchived: false }) : false,
      },
    }).catch(() => undefined);
    if (!value) return;

    const { data } = await archivedUsersApi.archive(this, [unique], value.reason);
    if (!data) return;

    await notifyResults(this, data, 'archive');
    await refreshUsers(this, [unique], data[0]?.status === 'Archived');
  }
}

export { ArchivedUsersArchiveEntityAction as api };
