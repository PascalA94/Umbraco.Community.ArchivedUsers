using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Security;

namespace Umbraco.Community.ArchivedUsers.Core.Services;

internal sealed partial class ArchivedUserService
{
    public async Task<int> ReconcileAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ArchivedUserRecord> records;
        using (ICoreScope scope = _scopeProvider.CreateCoreScope(autoComplete: true))
        {
            records = await _repository.GetAllAsync();
        }

        if (records.Count == 0)
        {
            return 0;
        }

        Dictionary<Guid, IUser> users = await GetUsersByKeyAsync(records.Select(r => r.UserKey));
        var removed = 0;
        foreach (ArchivedUserRecord record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            users.TryGetValue(record.UserKey, out IUser? user);
            if (user is not null && UserStateRules.IsDisabled(user))
            {
                continue;
            }

            using IDisposable userLock = await _locks.AcquireAsync(record.UserKey, cancellationToken);

            // Read again under the lock: an archive may have landed since the first read (review BS-2).
            user = await _userService.GetAsync(record.UserKey);
            if (user is not null && UserStateRules.IsDisabled(user))
            {
                continue;
            }

            if (await ReconcileStaleRecordAsync(user, record.UserKey))
            {
                removed++;
            }
        }

        return removed;
    }

    /// <summary>
    /// Loads users by key in chunks, so a large archive never sends one query with thousands of parameters
    /// (SQL Server allows about 2,100; review BQ-7).
    /// </summary>
    private async Task<Dictionary<Guid, IUser>> GetUsersByKeyAsync(IEnumerable<Guid> keys)
    {
        var users = new Dictionary<Guid, IUser>();
        foreach (Guid[] chunk in keys.Distinct().Chunk(KeyLookupChunkSize))
        {
            foreach (IUser user in await _userService.GetAsync(chunk))
            {
                users[user.Key] = user;
            }
        }

        return users;
    }

    private const int KeyLookupChunkSize = 500;
}
