using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Notifications;
using Umbraco.Community.ArchivedUsers.Core.Security;

namespace Umbraco.Community.ArchivedUsers.Core.Services;

internal sealed partial class ArchivedUserService
{
    private const int ArchivedViewWarningThreshold = 5000;

    public async Task<IReadOnlyList<UserArchiveResult>> UnarchiveAsync(Guid performerKey, ISet<Guid> userKeys, bool? enable, ArchiveSource source)
    {
        IUser? performer = await _userService.GetAsync(performerKey);
        if (!UserStateRules.CanPerform(performer))
        {
            return userKeys.Select(k => new UserArchiveResult(k, ArchiveResultStatus.NotPermitted)).ToList();
        }

        var results = new List<UserArchiveResult>(userKeys.Count);
        foreach (Guid key in userKeys)
        {
            // Published after the lock is released (review BS-3, see ArchiveAsync).
            UserUnarchivedNotification? unarchived = null;
            using (await _locks.AcquireAsync(key))
            {
                UserArchiveResult result;
                try
                {
                    (result, unarchived) = await UnarchiveOneAsync(performer!, key, enable, source);
                }
                catch (Exception ex)
                {
                    // Per user, never for the whole call (review C-6). The scope was not completed, so nothing changed.
                    _logger.LogError(ex, "Unarchiving user {UserKey} failed", key);
                    result = new UserArchiveResult(key, ArchiveResultStatus.Failed);
                }

                results.Add(result);
            }

            if (unarchived is not null)
            {
                await _events.PublishAsync(unarchived);
            }
        }

        return results;
    }

    private async Task<(UserArchiveResult Result, UserUnarchivedNotification? Notification)> UnarchiveOneAsync(IUser performer, Guid key, bool? enable, ArchiveSource source)
    {
        IUser? target = await _userService.GetAsync(key);
        UserArchiveResult? refused = await AuthoriseAsync(performer, key, target);
        if (refused is not null)
        {
            return (refused, null);
        }

        bool reEnabled = false;
        UserOperationStatus? coreStatus = null;
        using (ICoreScope scope = _scopeProvider.CreateCoreScope())
        {
            ArchivedUserRecord? record = await _repository.GetAsync(key);
            if (record is null)
            {
                scope.Complete();
                return (new UserArchiveResult(key, ArchiveResultStatus.NotArchived), null);
            }

            // AU-11: the request can only narrow the site policy, and never enables a user who was disabled
            // before they were archived.
            bool setting = _settings.CurrentValue.UnarchiveEnablesUser;
            bool shouldEnable = (enable ?? setting) && setting && !record.WasDisabledBefore;

            // Delete first: core's UserSaved notification is deferred to this scope's completion
            // (docs/spikes.md S2), so the F7 handler then finds no record and writes no duplicate row.
            await _repository.DeleteAsync(key);
            if (shouldEnable)
            {
                UserOperationStatus status = await _userService.EnableAsync(performer.Key, new HashSet<Guid> { key });
                reEnabled = status == UserOperationStatus.Success;
                if (!reEnabled)
                {
                    coreStatus = status;
                }
            }

            await _repository.InsertAuditAsync(new ArchiveAuditEntry(
                key, ArchiveAuditAction.Unarchived, performer.Key, _time.GetUtcNow().UtcDateTime, source, null, target!.Name, reEnabled));
            scope.Complete();
        }

        await WriteCoreAuditAsync(performer.Key, target, ArchiveAuditAction.Unarchived, source);
        return (
            new UserArchiveResult(key, ArchiveResultStatus.Unarchived, coreStatus),
            new UserUnarchivedNotification(new UserUnarchiveEvent(key, performer.Key, source, reEnabled), _messagesFactory.Get()));
    }

    public async Task<PagedModel<ArchivedUserView>> GetArchivedAsync(Guid performerKey, int skip, int take, string? filter, ArchivedUserOrder orderBy, Direction direction)
    {
        List<ArchivedUserView> views = await LoadVisibleViewsAsync(performerKey);

        IEnumerable<ArchivedUserView> query = views;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            string term = filter.Trim();
            query = query.Where(v =>
                (v.User.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (v.User.Email?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        query = (orderBy, direction) switch
        {
            (ArchivedUserOrder.Name, Direction.Ascending) => query.OrderBy(v => v.User.Name, StringComparer.OrdinalIgnoreCase),
            (ArchivedUserOrder.Name, _) => query.OrderByDescending(v => v.User.Name, StringComparer.OrdinalIgnoreCase),
            (_, Direction.Ascending) => query.OrderBy(v => v.Record.ArchivedUtc),
            _ => query.OrderByDescending(v => v.Record.ArchivedUtc),
        };

        List<ArchivedUserView> filtered = query.ToList();
        return new PagedModel<ArchivedUserView>(filtered.Count, filtered.Skip(skip).Take(take));
    }

    public async Task<ArchivedUserView?> GetArchivedUserAsync(Guid performerKey, Guid userKey)
    {
        IUser? performer = await _userService.GetAsync(performerKey);
        IUser? user = await _userService.GetAsync(userKey);
        if (performer is null || user is null || !UserVisibility.CanSee(performer, user))
        {
            return null;
        }

        ArchivedUserRecord? record = await ReadRecordAsync(userKey);
        if (record is null)
        {
            return null;
        }

        string? archivedBy = record.ArchivedByUserKey is { } by ? (await _userService.GetAsync(by))?.Name : null;
        return new ArchivedUserView(user, record, archivedBy, !UserStateRules.IsDisabled(user));
    }

    public async Task<HashSet<Guid>> GetArchivedKeysAsync()
    {
        using ICoreScope scope = _scopeProvider.CreateCoreScope(autoComplete: true);
        return await _repository.GetKeysAsync();
    }

    public async Task<bool> IsArchivedAsync(Guid userKey) => await ReadRecordAsync(userKey) is not null;

    /// <summary>
    /// Starts from the package table, not from FilterAsync, so the view never loads every user: read the records,
    /// load those users by key through IUserService, keep the visible ones. Stale records are flagged, never fixed,
    /// because GET must not change state (AU-03, AU-14).
    /// </summary>
    private async Task<List<ArchivedUserView>> LoadVisibleViewsAsync(Guid performerKey)
    {
        IUser? performer = await _userService.GetAsync(performerKey);
        if (performer is null)
        {
            return [];
        }

        IReadOnlyList<ArchivedUserRecord> records;
        using (ICoreScope scope = _scopeProvider.CreateCoreScope(autoComplete: true))
        {
            records = await _repository.GetAllAsync();
        }

        if (records.Count > ArchivedViewWarningThreshold)
        {
            _logger.LogWarning("{Count} archived users; the Archived view pages them in memory", records.Count);
        }

        if (records.Count == 0)
        {
            return [];
        }

        Dictionary<Guid, IUser> users = await GetUsersByKeyAsync(records.Select(r => r.UserKey));
        Dictionary<Guid, string?> performerNames = (await GetUsersByKeyAsync(records.Where(r => r.ArchivedByUserKey.HasValue).Select(r => r.ArchivedByUserKey!.Value)))
            .ToDictionary(kv => kv.Key, kv => kv.Value.Name);

        var views = new List<ArchivedUserView>(records.Count);
        foreach (ArchivedUserRecord record in records)
        {
            // An orphan record (user deleted outside the package) is skipped, not an error (Review Focus 3).
            if (!users.TryGetValue(record.UserKey, out IUser? user) || !UserVisibility.CanSee(performer, user))
            {
                continue;
            }

            string? archivedBy = record.ArchivedByUserKey is { } by ? performerNames.GetValueOrDefault(by) : null;
            views.Add(new ArchivedUserView(user, record, archivedBy, !UserStateRules.IsDisabled(user)));
        }

        return views;
    }
}
