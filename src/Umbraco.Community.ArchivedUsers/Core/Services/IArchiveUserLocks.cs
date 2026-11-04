namespace Umbraco.Community.ArchivedUsers.Core.Services;

/// <summary>Per-user in-process lock around archive and unarchive (P6). Disposing the handle releases it.</summary>
internal interface IArchiveUserLocks
{
    Task<IDisposable> AcquireAsync(Guid userKey, CancellationToken cancellationToken = default);
}
