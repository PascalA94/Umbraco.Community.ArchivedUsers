using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.ArchivedUsers.Core.Migrations;
using Umbraco.Community.ArchivedUsers.Core.Services;

namespace Umbraco.Community.ArchivedUsers.Core.Handlers;

/// <summary>
/// F7a (AU-03): removes archive records whose user is enabled or gone, once per start, on every node.
/// Startup, not on read, because GET must not change state (AU-14); the list filter already keeps enabled users
/// visible, so this is housekeeping, not the security control. Never fails startup.
/// </summary>
internal sealed class ReconcileOnStartupHandler : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IRuntimeState _runtimeState;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ArchivedUsersSchemaState _schema;
    private readonly ILogger<ReconcileOnStartupHandler> _logger;

    public ReconcileOnStartupHandler(IRuntimeState runtimeState, IServiceScopeFactory scopeFactory, ArchivedUsersSchemaState schema, ILogger<ReconcileOnStartupHandler> logger)
    {
        _runtimeState = runtimeState;
        _scopeFactory = scopeFactory;
        _schema = schema;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            return;
        }

        try
        {
            // On a fresh install the package tables don't exist yet when this runs, and core logs the failed query
            // at Error level. Nothing can be stale before the tables exist, so skip until the migration has run.
            if (!_schema.IsReady())
            {
                return;
            }

            using IServiceScope scope = _scopeFactory.CreateScope();
            int removed = await scope.ServiceProvider.GetRequiredService<IArchivedUserService>().ReconcileAsync(cancellationToken);
            if (removed > 0)
            {
                _logger.LogInformation("Archive reconciliation removed {Count} stale records", removed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Archive reconciliation skipped at startup: {ExceptionType}", ex.GetType().Name);
        }
    }
}
