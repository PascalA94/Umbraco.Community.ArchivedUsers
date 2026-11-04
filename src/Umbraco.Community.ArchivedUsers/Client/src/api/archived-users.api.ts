import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import {
  getArchivedUsers,
  getArchivedUsersById,
  getArchivedUsersCleanUpPreview,
  getArchivedUsersConfig,
  postArchivedUsersArchive,
  postArchivedUsersCleanUp,
  postArchivedUsersUnarchive,
} from './generated/sdk.gen.js';
import type { ArchivedUserOrder, DirectionModel as Direction } from './generated/types.gen.js';

export type * from './generated/types.gen.js';

// Every call goes through core's configured client, so the backoffice's own token handling applies.
const client = umbHttpClient;

export const archivedUsersApi = {
  config: (host: UmbControllerHost) => tryExecute(host, getArchivedUsersConfig({ client })),

  getOne: (host: UmbControllerHost, id: string) =>
    tryExecute(host, getArchivedUsersById({ client, path: { id } }), { disableNotifications: true }),

  list: (host: UmbControllerHost, query: { skip: number; take: number; filter?: string; orderBy?: ArchivedUserOrder; orderDirection?: Direction }) =>
    tryExecute(host, getArchivedUsers({ client, query })),
  /** The archive-status background reload: errors stay quiet, so a failing server can't stack toasts (review A2-4). */
  listQuietly: (host: UmbControllerHost, query: { skip: number; take: number }) =>
    tryExecute(host, getArchivedUsers({ client, query }), { disableNotifications: true }),

  archive: (host: UmbControllerHost, userIds: string[], reason?: string) =>
    tryExecute(host, postArchivedUsersArchive({ client, body: { userIds, reason: reason ?? null } })),

  unarchive: (host: UmbControllerHost, userIds: string[]) =>
    tryExecute(host, postArchivedUsersUnarchive({ client, body: { userIds, enable: null } })),

  previewCleanUp: (host: UmbControllerHost) => tryExecute(host, getArchivedUsersCleanUpPreview({ client })),

  cleanUp: (host: UmbControllerHost, reason?: string) =>
    tryExecute(host, postArchivedUsersCleanUp({ client, body: { reason: reason ?? null } })),
};
