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

        // The global gate has no dependencies; it resolves the scoped filter per request, and only for core's
        // GET filter/user, so no scoped state is captured (P2) and other actions pay nothing (review BS-5).
        builder.Services.AddScoped<ArchivedUserListFilter>();
        builder.Services.AddSingleton<ArchivedUserListWarningClock>();
        builder.Services.Configure<MvcOptions>(options => options.Filters.Add(new ArchivedUserListFilterGate()));
    }
}
