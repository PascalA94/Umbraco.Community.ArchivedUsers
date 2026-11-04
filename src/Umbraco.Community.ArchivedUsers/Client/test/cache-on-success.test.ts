import { describe, expect, it, vi } from 'vitest';
import { cacheOnSuccess } from '../src/archive-status/cache-on-success.js';

describe('cacheOnSuccess', () => {
  it('retries after a failed (undefined) result instead of caching it', async () => {
    const load = vi.fn().mockResolvedValueOnce(undefined).mockResolvedValueOnce({ ok: 1 });
    const get = cacheOnSuccess(load);
    expect(await get()).toBeUndefined();
    expect(await get()).toEqual({ ok: 1 });
    expect(load).toHaveBeenCalledTimes(2);
  });

  it('caches the first success and shares an in-flight request', async () => {
    const load = vi.fn().mockResolvedValue({ ok: 1 });
    const get = cacheOnSuccess(load);
    const [a, b] = await Promise.all([get(), get()]);
    await get();
    expect(a).toEqual({ ok: 1 });
    expect(b).toEqual({ ok: 1 });
    expect(load).toHaveBeenCalledTimes(1);
  });

  it('retries after a rejected request', async () => {
    const load = vi.fn().mockRejectedValueOnce(new Error('boot')).mockResolvedValueOnce({ ok: 2 });
    const get = cacheOnSuccess(load);
    await expect(get()).rejects.toThrow('boot');
    expect(await get()).toEqual({ ok: 2 });
  });
});
