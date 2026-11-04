/** Core's user states as the Management API returns them. */
export type UserState = 'Active' | 'Disabled' | 'LockedOut' | 'Invited' | 'Inactive' | 'All';

export interface CanArchiveInput {
  isSelf: boolean;
  state: UserState | null | undefined;
  isArchived: boolean;
  isStale: boolean;
  targetIsAdmin: boolean;
  targetIsSuperUser: boolean;
  performerIsAdmin: boolean;
}

/**
 * Whether to show Archive in the user editor. This only hides the action: the server checks every user
 * itself (AU-01), so a wrong answer here can never widen access (design 7.3, P12d).
 */
export function canArchive(u: CanArchiveInput): boolean {
  if (u.isSelf || u.state === 'Invited' || u.targetIsSuperUser) return false;
  if (u.targetIsAdmin && !u.performerIsAdmin) return false;
  // A stale record (archived but enabled) shows Archive again (AU-03).
  if (u.isArchived && !u.isStale) return false;
  return true;
}

/** D6: core's Delete works for disabled users who never logged in, so the confirm dialog says so. */
export function showNeverLoggedInHint(u: { state: UserState | null | undefined; lastLoginDate: string | null; isArchived: boolean }): boolean {
  return u.state === 'Disabled' && u.lastLoginDate === null && !u.isArchived;
}
