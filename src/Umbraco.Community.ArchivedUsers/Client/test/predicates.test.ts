import { describe, expect, it } from 'vitest';
import { canArchive, showNeverLoggedInHint, type CanArchiveInput } from '../src/conditions/predicates.js';

const base: CanArchiveInput = {
  isSelf: false,
  state: 'Active',
  isArchived: false,
  isStale: false,
  targetIsAdmin: false,
  targetIsSuperUser: false,
  performerIsAdmin: true,
};

describe('canArchive', () => {
  it('is false for self', () => expect(canArchive({ ...base, isSelf: true })).toBe(false));
  it('is false for an invited user', () => expect(canArchive({ ...base, state: 'Invited' })).toBe(false));
  it('is false when archived and not stale', () => expect(canArchive({ ...base, state: 'Disabled', isArchived: true })).toBe(false));
  it('is true when archived but stale (AU-03)', () => expect(canArchive({ ...base, isArchived: true, isStale: true })).toBe(true));
  it('is true for an active other user', () => expect(canArchive(base)).toBe(true));
  it('is true for a disabled other user', () => expect(canArchive({ ...base, state: 'Disabled' })).toBe(true));
  it('is false for the superuser', () => expect(canArchive({ ...base, targetIsSuperUser: true })).toBe(false));
  it('is false for an admin target when the performer is not an admin (P12d)', () =>
    expect(canArchive({ ...base, targetIsAdmin: true, performerIsAdmin: false })).toBe(false));
  it('is true for an admin target when the performer is an admin', () =>
    expect(canArchive({ ...base, targetIsAdmin: true, performerIsAdmin: true })).toBe(true));
});

describe('showNeverLoggedInHint', () => {
  it('is true only for disabled, never logged in, not archived', () =>
    expect(showNeverLoggedInHint({ state: 'Disabled', lastLoginDate: null, isArchived: false })).toBe(true));
  it('is false once logged in', () =>
    expect(showNeverLoggedInHint({ state: 'Disabled', lastLoginDate: '2026-01-01T00:00:00Z', isArchived: false })).toBe(false));
  it('is false when archived', () => expect(showNeverLoggedInHint({ state: 'Disabled', lastLoginDate: null, isArchived: true })).toBe(false));
  it('is false for an active never-logged-in user (D6 says disabled)', () =>
    expect(showNeverLoggedInHint({ state: 'Inactive', lastLoginDate: null, isArchived: false })).toBe(false));
});
