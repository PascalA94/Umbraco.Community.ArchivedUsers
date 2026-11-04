/** How long a loaded archived set is trusted before the next user editor reloads it (review C-9). */
export const STATUS_MAX_AGE_MS = 30_000;

/** True when the archived set has never loaded, or loaded at least STATUS_MAX_AGE_MS ago. */
export function needsRefresh(loadedAt: number | undefined, now: number, maxAgeMs: number = STATUS_MAX_AGE_MS): boolean {
  return loadedAt === undefined || now - loadedAt >= maxAgeMs;
}
