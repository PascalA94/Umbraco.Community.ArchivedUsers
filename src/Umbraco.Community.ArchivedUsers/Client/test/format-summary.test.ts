import { describe, expect, it } from 'vitest';
import { formatSummary } from '../src/results/format-summary.js';
import { SKIP_REASONS, summariseResults } from '../src/results/summarise.js';
import en from '../src/lang/en-gb.js';

// A translator over the real en-GB dictionary, so the test checks real copy.
const dict = en.archivedUsers as Record<string, string | ((...a: any[]) => string)>;
const t = (key: string, ...args: any[]) => {
  const value = dict[key.replace('archivedUsers_', '')];
  if (value === undefined) throw new Error('missing key ' + key);
  return typeof value === 'function' ? value(...args) : value;
};
const r = (status: string) => ({ id: crypto.randomUUID(), status });

describe('formatSummary (P12c)', () => {
  it('starts with "N archived, M skipped"', () => {
    const lines = formatSummary(summariseResults([r('Archived'), r('Archived'), r('Archived'), r('Self'), r('Invited')]), 'archive', t);
    expect(lines[0]).toBe('3 archived, 2 skipped');
  });

  it('formats every skip reason in a fixed order, each from its own key', () => {
    const results = [...SKIP_REASONS].reverse().map((reason) => r(reason === 'CoreRefused' ? 'CoreRefused:MissingUser' : reason));
    const lines = formatSummary(summariseResults(results), 'archive', t).slice(1);
    expect(lines).toHaveLength(8);
    expect(lines).toEqual([
      "1 skipped: you can't archive yourself",
      "1 skipped: invited users can't be archived",
      '1 skipped: already archived',
      '1 skipped: not archived',
      "1 skipped: you don't have permission",
      '1 skipped: user not found',
      '1 skipped: cancelled',
      '1 skipped: Umbraco refused the change',
    ]);
  });

  it('omits reasons with a zero count', () =>
    expect(formatSummary(summariseResults([r('Archived'), r('NotPermitted')]), 'archive', t)).toEqual([
      '1 archived, 1 skipped',
      "1 skipped: you don't have permission",
    ]));

  it('says unarchived in unarchive mode', () =>
    expect(formatSummary(summariseResults([r('Unarchived'), r('Unarchived'), r('NotArchived')]), 'unarchive', t)).toEqual([
      '2 unarchived, 1 skipped',
      '1 skipped: not archived',
    ]));

  it('lines contain counts and localised text only, never names (P8)', () => {
    const lines = formatSummary(summariseResults([r('Archived'), r('Self')]), 'archive', t);
    for (const line of lines) expect(line).toMatch(/^[0-9a-zA-Z ,:'.]+$/);
  });
});
