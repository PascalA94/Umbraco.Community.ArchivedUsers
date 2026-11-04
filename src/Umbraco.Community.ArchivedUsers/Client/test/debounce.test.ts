import { afterEach, describe, expect, it, vi } from 'vitest';
import { debounce } from '../src/archived-view/debounce.js';

describe('debounce (AU-13)', () => {
  afterEach(() => vi.useRealTimers());

  it('calls once, 300 ms after the last of five quick calls', () => {
    vi.useFakeTimers();
    const fn = vi.fn();
    const debounced = debounce(fn, 300);
    for (let i = 0; i < 5; i++) {
      if (i > 0) vi.advanceTimersByTime(20); // five calls within 100 ms
      debounced(i);
    }
    expect(fn).not.toHaveBeenCalled();
    vi.advanceTimersByTime(299);
    expect(fn).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1);
    expect(fn).toHaveBeenCalledTimes(1);
    expect(fn).toHaveBeenCalledWith(4);
  });
});
