import { describe, expect, it } from 'vitest';
import { summariseResults } from '../src/results/summarise.js';

const r = (status: string) => ({ id: crypto.randomUUID(), status });

describe('summariseResults', () => {
  it('counts done and skipped with a reason per skip', () =>
    expect(summariseResults([r('Archived'), r('Archived'), r('Self'), r('CoreRefused:CannotDisableInvitedUser')])).toEqual({
      done: 2,
      skipped: 2,
      skipReasons: { Self: 1, CoreRefused: 1 },
    }));
  it('counts Unarchived as done', () => expect(summariseResults([r('Unarchived')]).done).toBe(1));
  it('returns zeros for an empty list', () => expect(summariseResults([])).toEqual({ done: 0, skipped: 0, skipReasons: {} }));

  // Review C-7: these are not "Umbraco refused"; an unknown status must not be reported as one of them either.
  it('counts DisabledNotArchived and Failed under their own reasons', () =>
    expect(summariseResults([r('DisabledNotArchived'), r('Failed'), r('Failed')]).skipReasons).toEqual({ DisabledNotArchived: 1, Failed: 2 }));
});
