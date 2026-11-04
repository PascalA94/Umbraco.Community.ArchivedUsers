using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.ArchivedUsers.Core.Configuration;
using Umbraco.Community.ArchivedUsers.Core.Persistence;

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

        return builder;
    }
}
