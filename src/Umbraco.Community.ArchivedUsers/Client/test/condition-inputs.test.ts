import { describe, expect, it } from 'vitest';
import { toCanArchiveInput } from '../src/conditions/condition-inputs.js';

const adminGroup = 'admin-group';
const superUser = 'super-user';

describe('toCanArchiveInput (P8)', () => {
  const target = { unique: 'target', state: 'Active' as const, userGroupUniques: ['editor'] };
  const current = { unique: 'me', isAdmin: false };
  const status = { isArchived: true, isStale: false };

  it('detects self by unique', () =>
    expect(toCanArchiveInput({ ...target, unique: 'me' }, current, status, adminGroup, superUser).isSelf).toBe(true));
  it('targetIsAdmin is true only when the admin group is present', () => {
    expect(toCanArchiveInput(target, current, status, adminGroup, superUser).targetIsAdmin).toBe(false);
    expect(toCanArchiveInput({ ...target, userGroupUniques: ['editor', adminGroup] }, current, status, adminGroup, superUser).targetIsAdmin).toBe(true);
  });
  it('targetIsSuperUser is true only for the superuser unique', () => {
    expect(toCanArchiveInput(target, current, status, adminGroup, superUser).targetIsSuperUser).toBe(false);
    expect(toCanArchiveInput({ ...target, unique: superUser }, current, status, adminGroup, superUser).targetIsSuperUser).toBe(true);
  });
  it('passes status, state and performer through', () => {
    const input = toCanArchiveInput(target, { unique: 'me', isAdmin: true }, status, adminGroup, superUser);
    expect(input).toMatchObject({ isArchived: true, isStale: false, state: 'Active', performerIsAdmin: true });
  });
  it('compares uniques case-insensitively (GUIDs)', () =>
    expect(toCanArchiveInput({ ...target, unique: 'ME' }, current, status, adminGroup, superUser).isSelf).toBe(true));
});
