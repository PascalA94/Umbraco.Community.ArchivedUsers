import { describe, expect, it } from 'vitest';
import { latestOnly } from '../src/archive-status/latest-only.js';

// Review A2-5: a slow background reload that started earlier must not overwrite a newer one (for example the
// refresh an Archive action runs) when its response lands last.
describe('latestOnly', () => {
  it('lets only the most recently started load write', () => {
    const loads = latestOnly();
    const older = loads.start();
    const newer = loads.start();
    expect(loads.isLatest(older)).toBe(false);
    expect(loads.isLatest(newer)).toBe(true);
  });
});
