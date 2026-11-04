using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.ArchivedUsers.Core;
using Umbraco.Community.ArchivedUsers.Web.Filters;
using Umbraco.Community.ArchivedUsers.Web.OpenApi;

namespace Umbraco.Community.ArchivedUsers.Web;

/// <summary>The package's only composer.</summary>
public sealed class ArchivedUsersComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddArchivedUsersCore();
        builder.AddArchivedUsersOpenApi();

        // The filter is scoped and resolved per request through AddService, so it never captures scoped state (P2).
        builder.Services.AddScoped<ArchivedUserListFilter>();
        builder.Services.AddSingleton<ArchivedUserListWarningClock>();
        builder.Services.Configure<MvcOptions>(options => options.Filters.AddService<ArchivedUserListFilter>());
    }
}
