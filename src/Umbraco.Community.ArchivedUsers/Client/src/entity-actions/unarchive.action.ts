import { archivedUsersApi } from '../api/archived-users.api.js';
import { ARCHIVED_USERS_ARCHIVE_STATUS_CONTEXT } from '../archive-status/archive-status.context-token.js';
import { ARCHIVED_USERS_ARCHIVE_CONFIRM_MODAL } from '../modal/archive-confirm.modal-token.js';
import { notifyResults } from '../results/notify.js';
import { refreshUsers } from './refresh-user.js';
import { UmbEntityActionBase } from '@umbraco-cms/backoffice/entity-action';
import { umbOpenModal } from '@umbraco-cms/backoffice/modal';

/** F5: unarchive the user open in the editor. They stay disabled (D3). */
export class ArchivedUsersUnarchiveEntityAction extends UmbEntityActionBase<never> {
  override async execute() {
    const unique = this.args.unique;
    if (!unique) throw new Error('No user to unarchive.');

    const status = await this.getContext(ARCHIVED_USERS_ARCHIVE_STATUS_CONTEXT);
    const config = await status?.config();
    const value = await umbOpenModal(this, ARCHIVED_USERS_ARCHIVE_CONFIRM_MODAL, {
      data: { mode: 'unarchive', count: 1, requireReason: config?.requireReason ?? false },
    }).catch(() => undefined);
    if (!value) return;

    const { data } = await archivedUsersApi.unarchive(this, [unique]);
    if (!data) return;

    await notifyResults(this, data, 'unarchive');
    await refreshUsers(this, [unique], false);
  }
}

export { ArchivedUsersUnarchiveEntityAction as api };
