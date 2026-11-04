import { describe, expect, it } from 'vitest';
import { archivedByLabel } from '../src/archived-view/map-item.js';

const t = { system: '[archivedUsers_system]', removedUser: '[archivedUsers_removedUser]' };

describe('archivedByLabel (Review Focus 5: never a blank "Archived by")', () => {
  it('shows System when the system archived the user', () =>
    expect(archivedByLabel({ archivedBySystem: true, archivedByName: null }, t)).toBe('[archivedUsers_system]'));
  it('shows Removed user when the archiver no longer exists', () =>
    expect(archivedByLabel({ archivedBySystem: false, archivedByName: null }, t)).toBe('[archivedUsers_removedUser]'));
  it('shows the archiver name when known', () =>
    expect(archivedByLabel({ archivedBySystem: false, archivedByName: 'Sam Admin' }, t)).toBe('Sam Admin'));
  it('treats an empty name as a removed user, never a blank cell', () =>
    expect(archivedByLabel({ archivedBySystem: false, archivedByName: '' }, t)).toBe('[archivedUsers_removedUser]'));
});
