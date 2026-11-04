import { ARCHIVED_USERS_ARCHIVE_CONFIRM_MODAL_ALIAS } from './modal/archive-confirm.modal-token.js';
import { UMB_USER_ENTITY_TYPE } from '@umbraco-cms/backoffice/user';

// Aliases are permanent once released (design 7.3).
export const CAN_ARCHIVE_CONDITION = 'ArchivedUsers.Condition.User.CanArchive';
export const IS_ARCHIVED_CONDITION = 'ArchivedUsers.Condition.User.IsArchived';

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'globalContext',
    alias: 'ArchivedUsers.GlobalContext.ArchiveStatus',
    name: 'Archived Users Archive Status Context',
    api: () => import('./archive-status/archive-status.context.js'),
  },
  {
    type: 'condition',
    alias: CAN_ARCHIVE_CONDITION,
    name: 'Archived Users Can Archive Condition',
    api: () => import('./conditions/can-archive.condition.js'),
  },
  {
    type: 'condition',
    alias: IS_ARCHIVED_CONDITION,
    name: 'Archived Users Is Archived Condition',
    api: () => import('./conditions/is-archived.condition.js'),
  },
  {
    type: 'modal',
    alias: ARCHIVED_USERS_ARCHIVE_CONFIRM_MODAL_ALIAS,
    name: 'Archived Users Archive Confirm Modal',
    element: () => import('./modal/archive-confirm.element.js'),
  },
  {
    type: 'entityAction',
    kind: 'default',
    alias: 'ArchivedUsers.EntityAction.User.Archive',
    name: 'Archive User',
    weight: 450,
    forEntityTypes: [UMB_USER_ENTITY_TYPE],
    api: () => import('./entity-actions/archive.action.js'),
    meta: { icon: 'icon-box', label: '#archivedUsers_archive' },
    conditions: [{ alias: CAN_ARCHIVE_CONDITION }],
  },
  {
    type: 'entityAction',
    kind: 'default',
    alias: 'ArchivedUsers.EntityAction.User.Unarchive',
    name: 'Unarchive User',
    weight: 449,
    forEntityTypes: [UMB_USER_ENTITY_TYPE],
    api: () => import('./entity-actions/unarchive.action.js'),
    meta: { icon: 'icon-undo', label: '#archivedUsers_unarchive' },
    conditions: [{ alias: IS_ARCHIVED_CONDITION }],
  },
  {
    type: 'localization',
    alias: 'ArchivedUsers.Localization.En',
    name: 'Archived Users English',
    meta: { culture: 'en' },
    js: () => import('./lang/en.js'),
  },
  {
    type: 'localization',
    alias: 'ArchivedUsers.Localization.EnGb',
    name: 'Archived Users English (UK)',
    meta: { culture: 'en-gb' },
    js: () => import('./lang/en-gb.js'),
  },
];
