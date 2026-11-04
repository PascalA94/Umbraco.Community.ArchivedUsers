import { archivedUsersApi } from '../api/archived-users.api.js';
import { ARCHIVED_USERS_ARCHIVE_STATUS_CONTEXT } from '../archive-status/archive-status.context-token.js';
import { ARCHIVED_USERS_ARCHIVE_CONFIRM_MODAL } from '../modal/archive-confirm.modal-token.js';
import { notifyResults } from '../results/notify.js';
import { reloadCollection } from '../bulk-actions/reload-collection.js';
import { UmbCollectionActionBase } from '@umbraco-cms/backoffice/collection';
import { UmbContextConsumerController } from '@umbraco-cms/backoffice/context-api';
import { umbOpenModal } from '@umbraco-cms/backoffice/modal';

/**
 * F3: "Archive disabled users". Counts on the server, so it works with HideDisabledUsersInBackOffice on.
 * With none to archive, the package's own dialog says so and offers only Close (the generic confirm modal is
 * never used, AU-04). Sample names are passed as an array and rendered one text binding each.
 */
export class ArchivedUsersArchiveDisabledCollectionAction extends UmbCollectionActionBase {
  override async execute() {
    const status = await new UmbContextConsumerController(this, ARCHIVED_USERS_ARCHIVE_STATUS_CONTEXT).asPromise().catch(() => undefined);
    const config = await status?.config();
    const { data: preview } = await archivedUsersApi.previewCleanUp(this);
    if (!preview) return;

    const value = await umbOpenModal(this, ARCHIVED_USERS_ARCHIVE_CONFIRM_MODAL, {
      data: {
        mode: 'cleanUp',
        count: preview.count,
        sampleNames: preview.sample.map((s) => s.name),
        requireReason: config?.requireReason ?? false,
      },
    }).catch(() => undefined);
    if (!value || preview.count === 0) return;

    const { data } = await archivedUsersApi.cleanUp(this, value.reason);
    if (!data) return;

    await notifyResults(this, data, 'archive');
    await status?.refresh();
    await reloadCollection(this);
  }
}

export { ArchivedUsersArchiveDisabledCollectionAction as api };
