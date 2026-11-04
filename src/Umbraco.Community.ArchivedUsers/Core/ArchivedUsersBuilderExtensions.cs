using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.DependencyInjection;
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
        builder.Services.AddOptions<ArchivedUsersSettings>()
            .Bind(builder.Config.GetSection(ArchivedUsersSettings.SectionName))
            .ValidateOnStart();

        builder.Services.AddScoped<IArchivedUserRepository, ArchivedUserRepository>();
        builder.Services.AddScoped<IArchivedUserService, ArchivedUserService>();
        builder.Services.AddScoped<ISecurityStampRotator, BackOfficeSecurityStampRotator>();

        // One lock table for the whole process: per-user locks only work if every scope shares them (P6).
        builder.Services.AddSingleton<IArchiveUserLocks, ArchiveUserLocks>();
        builder.Services.TryAddSingleton(TimeProvider.System);

        return builder;
    }
}
