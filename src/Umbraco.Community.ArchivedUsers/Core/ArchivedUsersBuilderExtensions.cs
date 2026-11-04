using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.ArchivedUsers.Core.Handlers;
using Umbraco.Community.ArchivedUsers.Core.Configuration;
using Umbraco.Community.ArchivedUsers.Core.Persistence;
using Umbraco.Community.ArchivedUsers.Core.Services;

namespace Umbraco.Community.ArchivedUsers.Core;

public static class ArchivedUsersBuilderExtensions
{
    /// <summary>
    /// Registers the line-agnostic core: settings, repository, services and handlers. Everything scoped is
    /// registered scoped; nothing holds a scope, request or user in a static (P2).
    /// </summary>
    public static IUmbracoBuilder AddArchivedUsersCore(this IUmbracoBuilder builder)
    {
        // Idempotent: a second call would register every notification handler twice.
        if (builder.Services.Any(d => d.ServiceType == typeof(ArchivedUsersCoreMarker)))
        {
            return builder;
        }

        builder.Services.AddSingleton<ArchivedUsersCoreMarker>();
        builder.Services.AddOptions<ArchivedUsersSettings>()
            .Bind(builder.Config.GetSection(ArchivedUsersSettings.SectionName))
            .ValidateOnStart();

        builder.Services.AddScoped<IArchivedUserRepository, ArchivedUserRepository>();
        builder.Services.AddScoped<IArchivedUserService, ArchivedUserService>();
        builder.Services.AddScoped<ISecurityStampRotator, BackOfficeSecurityStampRotator>();

        // One lock table for the whole process: per-user locks only work if every scope shares them (P6).
        builder.Services.AddSingleton<IArchiveUserLocks, ArchiveUserLocks>();
        builder.Services.TryAddSingleton(TimeProvider.System);

        builder.AddNotificationAsyncHandler<UserSavedNotification, UserSavedArchiveSyncHandler>();
        builder.AddNotificationAsyncHandler<UserDeletedNotification, UserDeletedArchiveCleanupHandler>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, ReconcileOnStartupHandler>();
        builder.Services.AddTransient<ReconcileOnStartupHandler>();

        return builder;
    }
}

/// <summary>Marks that the core services are registered.</summary>
internal sealed class ArchivedUsersCoreMarker
{
}
