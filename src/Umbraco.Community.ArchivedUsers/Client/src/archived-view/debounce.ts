/** Trailing debounce. The Archived view's search waits 300 ms after the last keystroke (AU-13). */
export function debounce<T extends unknown[]>(fn: (...a: T) => void, ms: number): (...a: T) => void {
  let timer: ReturnType<typeof setTimeout> | undefined;
  return (...args: T) => {
    if (timer !== undefined) clearTimeout(timer);
    timer = setTimeout(() => {
      timer = undefined;
      fn(...args);
    }, ms);
  };
}
