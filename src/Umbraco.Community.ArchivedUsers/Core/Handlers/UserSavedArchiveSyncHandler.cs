using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Security;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Notifications;
using Umbraco.Community.ArchivedUsers.Core.Persistence;
using Umbraco.Community.ArchivedUsers.Core.Security;

namespace Umbraco.Community.ArchivedUsers.Core.Handlers;

/// <summary>
/// F7: an archived user enabled outside the package (core Enable, API, uSync, an external-login callback) loses
/// the archive flag, with an <c>Unarchived</c> / <c>CoreEnable</c> audit row. <c>EnableAsync</c> publishes
/// <c>UserSavedNotification</c> (docs/spikes.md S2). The package's own unarchive deletes its record before core
/// saves, so this handler then finds nothing and writes no duplicate row.
/// </summary>
internal sealed class UserSavedArchiveSyncHandler : INotificationAsyncHandler<UserSavedNotification>
{
    private readonly IArchivedUserRepository _repository;
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IBackOfficeSecurityAccessor _securityAccessor;
    private readonly IEventAggregator _events;
    private readonly IEventMessagesFactory _messagesFactory;
    private readonly TimeProvider _time;
    private readonly ILogger<UserSavedArchiveSyncHandler> _logger;

    public UserSavedArchiveSyncHandler(
        IArchivedUserRepository repository,
        ICoreScopeProvider scopeProvider,
        IBackOfficeSecurityAccessor securityAccessor,
        IEventAggregator events,
        IEventMessagesFactory messagesFactory,
        TimeProvider time,
        ILogger<UserSavedArchiveSyncHandler> logger)
    {
        _repository = repository;
        _scopeProvider = scopeProvider;
        _securityAccessor = securityAccessor;
        _events = events;
        _messagesFactory = messagesFactory;
        _time = time;
        _logger = logger;
    }

    public async Task HandleAsync(UserSavedNotification notification, CancellationToken cancellationToken)
    {
        foreach (IUser user in notification.SavedEntities.Where(u => !UserStateRules.IsDisabled(u)))
        {
            try
            {
                await ClearFlagAsync(user);
            }
            catch (Exception ex)
            {
                // Never break core's save. A missed record is caught by startup reconciliation (F7a), and the
                // list filter never hides an enabled user anyway (AU-03).
                _logger.LogWarning(ex, "Could not clear the archive flag for {UserKey}", user.Key);
            }
        }
    }

    private async Task ClearFlagAsync(IUser user)
    {
        // AU-07: the performer is the signed-in backoffice user when there is one, else the system.
        Guid? performer = _securityAccessor.BackOfficeSecurity?.CurrentUser?.Key;
        bool removed;
        using (ICoreScope scope = _scopeProvider.CreateCoreScope())
        {
            removed = await _repository.DeleteAsync(user.Key);
            if (removed)
            {
                await _repository.InsertAuditAsync(new ArchiveAuditEntry(
                    user.Key, ArchiveAuditAction.Unarchived, performer, _time.GetUtcNow().UtcDateTime, ArchiveSource.CoreEnable, null, user.Name, ReEnabled: true));
            }

            scope.Complete();
        }

        if (removed)
        {
            await _events.PublishAsync(new UserUnarchivedNotification(
                new UserUnarchiveEvent(user.Key, performer, ArchiveSource.CoreEnable, ReEnabled: true), _messagesFactory.Get()));
        }
    }
}
