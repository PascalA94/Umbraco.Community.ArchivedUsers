export type SkipReason =
  | 'Self'
  | 'Invited'
  | 'AlreadyArchived'
  | 'NotArchived'
  | 'NotPermitted'
  | 'NotFound'
  | 'Cancelled'
  | 'CoreRefused'
  | 'DisabledNotArchived'
  | 'Failed';

/** The fixed order skip reasons are listed in (P12c). */
export const SKIP_REASONS: readonly SkipReason[] = [
  'Self',
  'Invited',
  'AlreadyArchived',
  'NotArchived',
  'NotPermitted',
  'NotFound',
  'Cancelled',
  'CoreRefused',
  'DisabledNotArchived',
  'Failed',
];

export interface ArchiveResult {
  id: string;
  status: string;
  message?: string | null;
}

export interface ResultSummary {
  done: number;
  skipped: number;
  skipReasons: Partial<Record<SkipReason, number>>;
}

/** Counts per-user results. Results carry ids and statuses only, never names (AU-04). */
export function summariseResults(results: ArchiveResult[]): ResultSummary {
  const summary: ResultSummary = { done: 0, skipped: 0, skipReasons: {} };
  for (const result of results) {
    if (result.status === 'Archived' || result.status === 'Unarchived') {
      summary.done++;
      continue;
    }

    summary.skipped++;
    const reason = (result.status.split(':')[0] as SkipReason) ?? 'CoreRefused';
    const key: SkipReason = SKIP_REASONS.includes(reason) ? reason : 'CoreRefused';
    summary.skipReasons[key] = (summary.skipReasons[key] ?? 0) + 1;
  }

  return summary;
}
