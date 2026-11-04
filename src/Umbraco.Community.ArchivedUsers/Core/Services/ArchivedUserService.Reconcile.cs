using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Community.ArchivedUsers.Core.Models;

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

        Dictionary<Guid, IUser> users = (await _userService.GetAsync(records.Select(r => r.UserKey).ToArray())).ToDictionary(u => u.Key);
        var removed = 0;
        foreach (ArchivedUserRecord record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            users.TryGetValue(record.UserKey, out IUser? user);
            if (user is not null && user.UserState == UserState.Disabled)
            {
                continue;
            }

            using IDisposable userLock = await _locks.AcquireAsync(record.UserKey, cancellationToken);
            if (await ReconcileStaleRecordAsync(user, record.UserKey))
            {
                removed++;
            }
        }

        return removed;
    }
}
