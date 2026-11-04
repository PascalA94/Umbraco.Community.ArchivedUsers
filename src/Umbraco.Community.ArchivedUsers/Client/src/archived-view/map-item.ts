import type { ArchivedUserResponseModel } from '../api/archived-users.api.js';

export interface ArchivedUserCollectionItem {
  unique: string;
  entityType: 'user';
  name: string;
  email: string;
  archivedUtc: string;
  /** Null for the system, or for an archiver who no longer exists ("Removed user"). */
  archivedByName: string | null;
  archivedBySystem: boolean;
  reason: string | null;
  lastLoginDate: string | null;
  /** Archived but enabled (AU-03). */
  stale: boolean;
}

/** Maps the API model. entityType is core's "user", so core's user entity actions and our Unarchive bulk action apply. */
export function mapItem(m: ArchivedUserResponseModel): ArchivedUserCollectionItem {
  return {
    unique: m.user.id,
    entityType: 'user',
    name: m.user.name,
    email: m.user.email,
    archivedUtc: m.archivedUtc,
    archivedByName: m.archivedBy?.name ?? null,
    archivedBySystem: m.archivedBy == null,
    reason: m.reason ?? null,
    lastLoginDate: m.user.lastLoginDate ?? null,
    stale: m.stale,
  };
}
