using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.AuthorizationStatus;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Community.ArchivedUsers.Core.Configuration;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Notifications;
using Umbraco.Community.ArchivedUsers.Core.Persistence;
using Umbraco.Community.ArchivedUsers.Core.Security;

namespace Umbraco.Community.ArchivedUsers.Core.Services;

/// <summary>
/// Archive and unarchive users through core services only. Never writes <c>umbracoUser</c> directly.
/// Logs carry user keys only, never names, emails or reasons (AU-09, AU-12).
/// </summary>
internal sealed partial class ArchivedUserService : IArchivedUserService
{
    private const string AuditEntityType = "User";

    private readonly IUserService _userService;
    private readonly IUserPermissionService _permissions;
    private readonly ISecurityStampRotator _stampRotator;
    private readonly IAuditService _auditService;
    private readonly IArchivedUserRepository _repository;
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IEventAggregator _events;
    private readonly IEventMessagesFactory _messagesFactory;
    private readonly IArchiveUserLocks _locks;
    private readonly TimeProvider _time;
    private readonly IOptionsMonitor<ArchivedUsersSettings> _settings;
    private readonly ILogger<ArchivedUserService> _logger;

    public ArchivedUserService(
        IUserService userService,
        IUserPermissionService permissions,
        ISecurityStampRotator stampRotator,
        IAuditService auditService,
        IArchivedUserRepository repository,
        ICoreScopeProvider scopeProvider,
        IEventAggregator events,
        IEventMessagesFactory messagesFactory,
        IArchiveUserLocks locks,
        TimeProvider time,
        IOptionsMonitor<ArchivedUsersSettings> settings,
        ILogger<ArchivedUserService> logger)
    {
        _userService = userService;
        _permissions = permissions;
        _stampRotator = stampRotator;
        _auditService = auditService;
        _repository = repository;
        _scopeProvider = scopeProvider;
        _events = events;
        _messagesFactory = messagesFactory;
        _locks = locks;
        _time = time;
        _settings = settings;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UserArchiveResult>> ArchiveAsync(Guid performerKey, ISet<Guid> userKeys, string? reason, ArchiveSource source)
    {
        // Step 0 (P9): a missing, disabled or locked-out performer, for example an admin archived while their access token
        // is still live, may change nobody. Core is never called.
        IUser? performer = await _userService.GetAsync(performerKey);
        if (!UserStateRules.CanPerform(performer))
        {
            return userKeys.Select(k => new UserArchiveResult(k, ArchiveResultStatus.NotPermitted)).ToList();
        }

        var results = new List<UserArchiveResult>(userKeys.Count);
        foreach (Guid key in userKeys)
        {
            // One lock per user, held from the state read to the record write (P6). Never two at once.
            // The Archived notification is published after the lock is released: the stripes are not re-entrant,
            // so a subscriber that archives any user on the same stripe would otherwise deadlock (review BS-3).
            UserArchivedNotification? archived = null;
            using (await _locks.AcquireAsync(key))
            {
                UserArchiveResult result;
                try
                {
                    (result, archived) = await ArchiveOneAsync(performer!, key, reason, source);
                }
                catch (Exception ex)
                {
                    // Per user, never for the whole call: the other users keep their results (review C-6). Logged by
                    // key and exception type only: exception text can carry SQL parameters such as names (A2-2).
                    _logger.LogError("Archiving user {UserKey} failed ({ExceptionType})", key, ex.GetType().Name);
                    result = await ResultAfterArchiveFailureAsync(key);
                }

                results.Add(result);
            }

            if (archived is not null)
            {
                try
                {
                    await _events.PublishAsync(archived);
                }
                catch (Exception ex)
                {
                    // The user is archived; a failing subscriber must not fail the rest of the batch (A2-3).
                    _logger.LogError("A UserArchived subscriber failed for {UserKey} ({ExceptionType})", key, ex.GetType().Name);
                }
            }
        }

        return results;
    }

    private async Task<(UserArchiveResult Result, UserArchivedNotification? Notification)> ArchiveOneAsync(IUser performer, Guid key, string? reason, ArchiveSource source)
    {
        IUser? target = await _userService.GetAsync(key);
        UserArchiveResult? refused = await AuthoriseAsync(performer, key, target);
        if (refused is not null)
        {
            return (refused, null);
        }

        ArchivedUserRecord? record = await ReadRecordAsync(key);
        if (record is not null)
        {
            if (UserStateRules.IsDisabled(target!))
            {
                return (new UserArchiveResult(key, ArchiveResultStatus.AlreadyArchived), null);
            }

            // AU-03: a record for an enabled user is stale. Reconcile it first, then archive as new. The person
            // archiving triggered it, so they are the audit row's performer (AU-07, review BS-4).
            await ReconcileStaleRecordAsync(target, performerKey: performer.Key);
        }

        if (target!.UserState == UserState.Invited)
        {
            return (new UserArchiveResult(key, ArchiveResultStatus.Invited), null);
        }

        var archiveEvent = new UserArchiveEvent(key, performer.Key, source, reason);
        var archiving = new UserArchivingNotification(archiveEvent, _messagesFactory.Get());
        if (await _events.PublishCancelableAsync(archiving))
        {
            return (new UserArchiveResult(key, ArchiveResultStatus.Cancelled, Message: archiving.Messages.GetAll().FirstOrDefault()?.Message), null);
        }

        bool wasDisabledBefore = UserStateRules.IsDisabled(target);
        if (!wasDisabledBefore)
        {
            // One user per call: core returns a single status per call, so this is how bulk gets per-user results.
            UserOperationStatus status = await _userService.DisableAsync(performer.Key, new HashSet<Guid> { key });
            if (status != UserOperationStatus.Success)
            {
                return (new UserArchiveResult(key, ArchiveResultStatus.CoreRefused, status), null);
            }

            await RotateStampAsync(key);
        }

        var newRecord = new ArchivedUserRecord(key, _time.GetUtcNow().UtcDateTime, performer.Key, source, reason, wasDisabledBefore);
        try
        {
            using ICoreScope scope = _scopeProvider.CreateCoreScope();
            if (!await _repository.TryInsertAsync(newRecord))
            {
                scope.Complete();
                _logger.LogWarning("Archive record for {UserKey} already existed when inserting; another request won", key);
                return (new UserArchiveResult(key, ArchiveResultStatus.AlreadyArchived), null);
            }

            await _repository.InsertAuditAsync(new ArchiveAuditEntry(
                key, ArchiveAuditAction.Archived, performer.Key, newRecord.ArchivedUtc, source, reason, target.Name));
            scope.Complete();
        }
        catch (Exception ex)
        {
            // Per user, never for the whole call: the other users keep their results (review BQ-5).
            if (!wasDisabledBefore)
            {
                // Accepted gap (docs/design.md 7.2): disabled but not archived. Safe, because the user is disabled
                // and still visible; a retry archives them with wasDisabledBefore = true. The status says so, so the
                // admin is not told Umbraco refused (review C-7).
                _logger.LogError("User {UserKey} was disabled but not archived ({ExceptionType})", key, ex.GetType().Name);
                return (new UserArchiveResult(key, ArchiveResultStatus.DisabledNotArchived), null);
            }

            _logger.LogError("User {UserKey} could not be archived ({ExceptionType})", key, ex.GetType().Name);
            return (new UserArchiveResult(key, ArchiveResultStatus.Failed), null);
        }

        await WriteCoreAuditAsync(performer.Key, target, ArchiveAuditAction.Archived, source);
        return (new UserArchiveResult(key, ArchiveResultStatus.Archived), new UserArchivedNotification(archiveEvent, _messagesFactory.Get()));
    }

    /// <summary>
    /// After an unexpected failure, report what is true now rather than guess (review A2-1): core can commit a disable
    /// and then throw, for example from a third-party UserSaved handler after the commit.
    /// </summary>
    private async Task<UserArchiveResult> ResultAfterArchiveFailureAsync(Guid key)
    {
        try
        {
            IUser? user = await _userService.GetAsync(key);
            if (user is null || !UserStateRules.IsDisabled(user))
            {
                return new UserArchiveResult(key, ArchiveResultStatus.Failed);
            }

            return await ReadRecordAsync(key) is null
                ? new UserArchiveResult(key, ArchiveResultStatus.DisabledNotArchived)
                : new UserArchiveResult(key, ArchiveResultStatus.Archived);
        }
        catch (Exception)
        {
            return new UserArchiveResult(key, ArchiveResultStatus.Failed);
        }
    }

    /// <summary>
    /// AU-01 and AU-16. <c>IUserService</c> does not authorise (docs/design.md section 4), so every write is
    /// checked here: unknown keys, self, the superuser, core's own admin rule and the visibility rule.
    /// </summary>
    private async Task<UserArchiveResult?> AuthoriseAsync(IUser performer, Guid key, IUser? target)
    {
        if (target is null)
        {
            return new UserArchiveResult(key, UserVisibility.IsAdmin(performer) ? ArchiveResultStatus.NotFound : ArchiveResultStatus.NotPermitted);
        }

        if (key == performer.Key)
        {
            return new UserArchiveResult(key, ArchiveResultStatus.Self);
        }

        if (UserVisibility.IsSuperUser(key)
            || await _permissions.AuthorizeAccessAsync(performer, new[] { key }) != UserAuthorizationStatus.Success
            || !UserVisibility.CanSee(performer, target))
        {
            return new UserArchiveResult(key, ArchiveResultStatus.NotPermitted);
        }

        return null;
    }

    private async Task<ArchivedUserRecord?> ReadRecordAsync(Guid key)
    {
        using ICoreScope scope = _scopeProvider.CreateCoreScope(autoComplete: true);
        return await _repository.GetAsync(key);
    }

    /// <summary>
    /// Removes a record whose user is not disabled (or no longer exists) and audits it as Reconcile. The audit row is
    /// written only when this call's delete removed the row, so concurrent nodes write one row between them (AU-14b).
    /// Shared with <c>ReconcileAsync</c>.
    /// </summary>
    private async Task<bool> ReconcileStaleRecordAsync(IUser? user, Guid? missingUserKey = null, Guid? performerKey = null)
    {
        Guid key = user?.Key ?? missingUserKey!.Value;
        using ICoreScope scope = _scopeProvider.CreateCoreScope();
        bool removed = await _repository.DeleteAsync(key);
        if (removed)
        {
            await _repository.InsertAuditAsync(new ArchiveAuditEntry(
                key, ArchiveAuditAction.Unarchived, performerKey, _time.GetUtcNow().UtcDateTime, ArchiveSource.Reconcile, null, user?.Name));
        }

        scope.Complete();
        return removed;
    }

    private async Task RotateStampAsync(Guid key)
    {
        try
        {
            await _stampRotator.RotateAsync(key);
        }
        catch (Exception ex)
        {
            // AU-02: non-fatal. Core already revoked the user's tokens on disable, and from the 17.8.0 / 18.3.0
            // floors the authorize flow refuses disabled users; the stamp is the third layer.
            _logger.LogWarning(ex, "Security stamp rotation failed for {UserKey}", key);
        }
    }

    /// <summary>AU-07: a second record outside the package's tables. Action and source only, never reason or name.</summary>
    private async Task WriteCoreAuditAsync(Guid performerKey, IUser target, ArchiveAuditAction action, ArchiveSource source)
    {
        try
        {
            await _auditService.AddAsync(AuditType.Custom, performerKey, target.Id, AuditEntityType, $"ArchivedUsers: {action} ({source})");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Core audit entry could not be written for {UserKey}", target.Key);
        }
    }
}
