/**
 * Memoises an async loader, but only its successes: an undefined result or a rejection is not cached, so the next
 * call tries again. A request made while the backoffice is still signing in can fail; caching that failure would
 * hide the Archive action for the whole session (Task 18 finding). Concurrent callers share one request.
 */
export function cacheOnSuccess<T>(load: () => Promise<T | undefined>): () => Promise<T | undefined> {
  let value: T | undefined;
  let pending: Promise<T | undefined> | undefined;
  return () => {
    if (value !== undefined) return Promise.resolve(value);
    pending ??= load()
      .then((result) => {
        value = result ?? undefined;
        return result;
      })
      .finally(() => (pending = undefined));
    return pending;
  };
}
