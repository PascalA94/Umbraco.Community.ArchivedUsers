namespace Umbraco.Community.ArchivedUsers.Core.Services;

/// <summary>
/// 64 striped <see cref="SemaphoreSlim"/>s keyed by user key, registered as a DI singleton (never a static).
/// Closes the same-node race in which two concurrent archives of one user could store a wrong
/// <c>wasDisabledBefore</c> (docs/design.md 7.2, P6). Across nodes the unique index still guarantees one record.
/// Not re-entrant, and striped: a handler of <c>UserArchivingNotification</c> must not archive or unarchive any
/// user (another user can share the stripe). The Archived and Unarchived notifications are published after release.
/// </summary>
internal sealed class ArchiveUserLocks : IArchiveUserLocks
{
    private const int StripeCount = 64;

    private readonly SemaphoreSlim[] _stripes = Enumerable.Range(0, StripeCount).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public async Task<IDisposable> AcquireAsync(Guid userKey, CancellationToken cancellationToken = default)
    {
        SemaphoreSlim stripe = _stripes[(userKey.GetHashCode() & int.MaxValue) % StripeCount];
        await stripe.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(stripe);
    }

    private sealed class Releaser(SemaphoreSlim stripe) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                stripe.Release();
            }
        }
    }
}
