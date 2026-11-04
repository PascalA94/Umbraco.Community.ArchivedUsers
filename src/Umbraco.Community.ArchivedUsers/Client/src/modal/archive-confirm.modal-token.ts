import { UmbModalToken } from '@umbraco-cms/backoffice/modal';

export interface ArchiveConfirmModalData {
  mode: 'archive' | 'unarchive' | 'cleanUp';
  count: number;
  /** User names. Rendered with text bindings only, never as HTML (AU-04). */
  sampleNames?: string[];
  showNeverLoggedInHint?: boolean;
  requireReason: boolean;
}

export interface ArchiveConfirmModalValue {
  reason?: string;
}

export const ARCHIVED_USERS_ARCHIVE_CONFIRM_MODAL_ALIAS = 'ArchivedUsers.Modal.ArchiveConfirm';

/** The package's only confirm dialog; core's generic confirm modal renders its content as HTML and is not used (AU-04). */
export const ARCHIVED_USERS_ARCHIVE_CONFIRM_MODAL = new UmbModalToken<ArchiveConfirmModalData, ArchiveConfirmModalValue>(
  ARCHIVED_USERS_ARCHIVE_CONFIRM_MODAL_ALIAS,
  { modal: { type: 'dialog', size: 'small' } },
);
