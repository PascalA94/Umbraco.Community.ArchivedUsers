import { describe, expect, it } from 'vitest';
import { mapItem } from '../src/archived-view/map-item.js';
import type { ArchivedUserResponseModel } from '../src/api/archived-users.api.js';

const model = (over: Partial<ArchivedUserResponseModel> = {}): ArchivedUserResponseModel =>
  ({
    user: { id: 'u1', name: 'Jo Bloggs', email: 'jo@example.test', lastLoginDate: '2026-01-02T03:04:05Z' },
    archivedUtc: '2026-10-24T09:00:00Z',
    archivedBy: { id: 'a1', name: 'Sam Admin' },
    reason: 'Left 24 Oct',
    source: 'Manual',
    stale: false,
    ...over,
  }) as unknown as ArchivedUserResponseModel;

describe('mapItem', () => {
  it('maps the user fields', () =>
    expect(mapItem(model())).toMatchObject({ unique: 'u1', name: 'Jo Bloggs', email: 'jo@example.test', lastLoginDate: '2026-01-02T03:04:05Z' }));
  it('uses entityType user so the Unarchive bulk action applies', () => expect(mapItem(model()).entityType).toBe('user'));
  it('maps a null archivedBy to the system', () =>
    expect(mapItem(model({ archivedBy: null }))).toMatchObject({ archivedBySystem: true, archivedByName: null }));
  it('maps a removed archiver to a null name, not the system (Review Focus 5)', () =>
    expect(mapItem(model({ archivedBy: { id: 'gone', name: null } }))).toMatchObject({ archivedBySystem: false, archivedByName: null }));
  it('passes stale through (AU-03)', () => expect(mapItem(model({ stale: true })).stale).toBe(true));
  it('passes reason and archived date through', () =>
    expect(mapItem(model())).toMatchObject({ reason: 'Left 24 Oct', archivedUtc: '2026-10-24T09:00:00Z', archivedByName: 'Sam Admin' }));
});
