import { UMB_COLLECTION_ALIAS_CONDITION } from '@umbraco-cms/backoffice/collection';
import { UMB_WORKSPACE_CONDITION_ALIAS } from '@umbraco-cms/backoffice/workspace';

export const ARCHIVED_USERS_ROOT_ENTITY_TYPE = 'archived-users-root';
export const ARCHIVED_USERS_WORKSPACE_ALIAS = 'ArchivedUsers.Workspace.Root';
export const ARCHIVED_USER_COLLECTION_ALIAS = 'ArchivedUsers.Collection.ArchivedUser';
export const ARCHIVED_USER_COLLECTION_REPOSITORY_ALIAS = 'ArchivedUsers.Repository.ArchivedUserCollection';

// F4. Aliases are permanent once released (design 7.3).
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'menuItem',
    alias: 'ArchivedUsers.MenuItem.ArchivedUsers',
    name: 'Archived Users Menu Item',
    // Just below core's Users (200), so it sits under Users. The menu constant is not publicly exported (S6).
    weight: 190,
    meta: { label: '#archivedUsers_archivedUsers', icon: 'icon-box', entityType: ARCHIVED_USERS_ROOT_ENTITY_TYPE, menus: ['Umb.Menu.UserManagement'] },
  },
  {
    type: 'workspace',
    kind: 'default',
    alias: ARCHIVED_USERS_WORKSPACE_ALIAS,
    name: 'Archived Users Root Workspace',
    meta: { entityType: ARCHIVED_USERS_ROOT_ENTITY_TYPE, headline: '#archivedUsers_archivedUsers' },
  },
  {
    type: 'workspaceView',
    kind: 'collection',
    alias: 'ArchivedUsers.WorkspaceView.Root.Collection',
    name: 'Archived Users Root Collection Workspace View',
    meta: { label: '#archivedUsers_archivedUsers', pathname: 'collection', icon: 'icon-box', collectionAlias: ARCHIVED_USER_COLLECTION_ALIAS },
    conditions: [{ alias: UMB_WORKSPACE_CONDITION_ALIAS, match: ARCHIVED_USERS_WORKSPACE_ALIAS }],
  },
  {
    type: 'collection',
    kind: 'default',
    alias: ARCHIVED_USER_COLLECTION_ALIAS,
    name: 'Archived Users Collection',
    meta: { repositoryAlias: ARCHIVED_USER_COLLECTION_REPOSITORY_ALIAS },
  },
  {
    type: 'repository',
    alias: ARCHIVED_USER_COLLECTION_REPOSITORY_ALIAS,
    name: 'Archived Users Collection Repository',
    api: () => import('./archived-user-collection.repository.js'),
  },
  {
    type: 'collectionView',
    alias: 'ArchivedUsers.CollectionView.ArchivedUser.Table',
    name: 'Archived Users Table Collection View',
    element: () => import('./archived-user-table.element.js'),
    meta: { label: 'Table', icon: 'icon-table', pathName: 'table' },
    conditions: [{ alias: UMB_COLLECTION_ALIAS_CONDITION, match: ARCHIVED_USER_COLLECTION_ALIAS }],
  },
];
