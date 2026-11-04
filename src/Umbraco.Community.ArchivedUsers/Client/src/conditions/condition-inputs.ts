import type { CanArchiveInput, UserState } from './predicates.js';

const same = (a: string, b: string) => a.toLowerCase() === b.toLowerCase();

/** The CanArchive condition's glue as a pure function, so it can be tested without the backoffice (P8). */
export function toCanArchiveInput(
  target: { unique: string; state: UserState | null | undefined; userGroupUniques: string[] },
  current: { unique: string; isAdmin: boolean },
  status: { isArchived: boolean; isStale: boolean },
  adminGroupUnique: string,
  superUserUnique: string,
): CanArchiveInput {
  return {
    isSelf: same(target.unique, current.unique),
    state: target.state,
    isArchived: status.isArchived,
    isStale: status.isStale,
    targetIsAdmin: target.userGroupUniques.some((g) => same(g, adminGroupUnique)),
    targetIsSuperUser: same(target.unique, superUserUnique),
    performerIsAdmin: current.isAdmin,
  };
}
