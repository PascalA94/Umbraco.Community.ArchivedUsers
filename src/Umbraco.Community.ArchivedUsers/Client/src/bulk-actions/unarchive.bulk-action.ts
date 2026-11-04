import { archivedUsersApi } from '../api/archived-users.api.js';
import { ARCHIVED_USERS_ARCHIVE_STATUS_CONTEXT } from '../archive-status/archive-status.context-token.js';
import { ARCHIVED_USERS_ARCHIVE_CONFIRM_MODAL } from '../modal/archive-confirm.modal-token.js';
import { notifyResults } from '../results/notify.js';
import { reloadCollection } from './reload-collection.js';
import { UmbEntityBulkActionBase } from '@umbraco-cms/backoffice/entity-bulk-action';
import { umbOpenModal } from '@umbraco-cms/backoffice/modal';

/** F5: unarchive the rows selected in the Archived users view. They stay disabled (D3). */
export class ArchivedUsersUnarchiveBulkAction extends UmbEntityBulkActionBase<object> {
  override async execute() {
    if (!this.selection.length) return;
    const status = await this.getContext(ARCHIVED_USERS_ARCHIVE_STATUS_CONTEXT);
    const config = await status?.config();

    const value = await umbOpenModal(this, ARCHIVED_USERS_ARCHIVE_CONFIRM_MODAL, {
      data: { mode: 'unarchive', count: this.selection.length, requireReason: config?.requireReason ?? false },
    }).catch(() => undefined);
    if (!value) return;

    const { data } = await archivedUsersApi.unarchive(this, [...this.selection]);
    if (!data) return;

    await notifyResults(this, data, 'unarchive');
    await status?.refresh();
    await reloadCollection(this);
  }
}

export { ArchivedUsersUnarchiveBulkAction as api };
