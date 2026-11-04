/** Lets only the most recently started load write its result, so an older, slower response is dropped (review A2-5). */
export function latestOnly(): { start: () => number; isLatest: (token: number) => boolean } {
  let current = 0;
  return {
    start: () => ++current,
    isLatest: (token) => token === current,
  };
}
