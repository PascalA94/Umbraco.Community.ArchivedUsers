import { describe, expect, it } from 'vitest';
import { needsRefresh, STATUS_MAX_AGE_MS } from '../src/archive-status/freshness.js';

// Review C-9: the archived set is reloaded when it is missing or older than the limit, so a change made by another
// admin shows the next time a user is opened, without a full page reload.
describe('needsRefresh', () => {
  it('loads when nothing has loaded yet (including after a failed first load)', () => expect(needsRefresh(undefined, 1_000)).toBe(true));
  it('keeps a fresh load', () => expect(needsRefresh(1_000, 1_000 + STATUS_MAX_AGE_MS - 1)).toBe(false));
  it('reloads once the load is as old as the limit', () => expect(needsRefresh(1_000, 1_000 + STATUS_MAX_AGE_MS)).toBe(true));
  it('limits the age to 30 seconds', () => expect(STATUS_MAX_AGE_MS).toBe(30_000));
});
