import { SKIP_REASONS, type ResultSummary, type SkipReason } from './summarise.js';

type Translate = (key: string, ...args: any[]) => string;

/** One literal key per reason: localisation keys are never built from data (P11). */
const SKIP_LINE: Record<SkipReason, (t: Translate, count: number) => string> = {
  Self: (t, n) => t('archivedUsers_skipSelf', n),
  Invited: (t, n) => t('archivedUsers_skipInvited', n),
  AlreadyArchived: (t, n) => t('archivedUsers_skipAlreadyArchived', n),
  NotArchived: (t, n) => t('archivedUsers_skipNotArchived', n),
  NotPermitted: (t, n) => t('archivedUsers_skipNotPermitted', n),
  NotFound: (t, n) => t('archivedUsers_skipNotFound', n),
  Cancelled: (t, n) => t('archivedUsers_skipCancelled', n),
  CoreRefused: (t, n) => t('archivedUsers_skipCoreRefused', n),
};

/**
 * Notification lines: "N archived, M skipped", then one line per skip reason in a fixed order (P12c).
 * Lines carry counts and localised text only, never names (P8).
 */
export function formatSummary(s: ResultSummary, mode: 'archive' | 'unarchive', t: Translate): string[] {
  const done = mode === 'archive' ? t('archivedUsers_archivedCount', s.done) : t('archivedUsers_unarchivedCount', s.done);
  const lines = [s.skipped > 0 ? `${done}, ${t('archivedUsers_skippedCount', s.skipped)}` : done];
  for (const reason of SKIP_REASONS) {
    const count = s.skipReasons[reason];
    if (count) lines.push(SKIP_LINE[reason](t, count));
  }

  return lines;
}
