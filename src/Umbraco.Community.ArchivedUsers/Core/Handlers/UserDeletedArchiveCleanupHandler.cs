using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Community.ArchivedUsers.Core.Migrations;
using Umbraco.Community.ArchivedUsers.Core.Persistence;

namespace Umbraco.Community.ArchivedUsers.Core.Handlers;

/// <summary>F7: a user deleted through core loses the archive record. Audit rows stay on purpose (D7, no FK).</summary>
internal sealed class UserDeletedArchiveCleanupHandler : INotificationAsyncHandler<UserDeletedNotification>
{
    private readonly IArchivedUserRepository _repository;
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly ArchivedUsersSchemaState _schema;
    private readonly ILogger<UserDeletedArchiveCleanupHandler> _logger;

    public UserDeletedArchiveCleanupHandler(IArchivedUserRepository repository, ICoreScopeProvider scopeProvider, ArchivedUsersSchemaState schema, ILogger<UserDeletedArchiveCleanupHandler> logger)
    {
        _repository = repository;
        _scopeProvider = scopeProvider;
        _schema = schema;
        _logger = logger;
    }

    public async Task HandleAsync(UserDeletedNotification notification, CancellationToken cancellationToken)
    {
        if (!_schema.IsReady())
        {
            return;
        }

        foreach (IUser user in notification.DeletedEntities)
        {
            try
            {
                using ICoreScope scope = _scopeProvider.CreateCoreScope();
                await _repository.DeleteAsync(user.Key);
                scope.Complete();
            }
            catch (Exception ex)
            {
                // An orphan record is harmless: the Archived view skips it and reconciliation removes it.
                _logger.LogWarning(ex, "Could not remove the archive record of deleted user {UserKey}", user.Key);
            }
        }
    }
}
