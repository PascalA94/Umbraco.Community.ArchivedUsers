import type { ArchivedUsersArchiveStatusContext } from './archive-status.context.js';
import { UmbContextToken } from '@umbraco-cms/backoffice/context-api';

export const ARCHIVED_USERS_ARCHIVE_STATUS_CONTEXT = new UmbContextToken<ArchivedUsersArchiveStatusContext>('ArchivedUsers.GlobalContext.ArchiveStatus');
