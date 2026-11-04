using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.ArchivedUsers.Web.Controllers;

namespace Umbraco.Community.ArchivedUsers.Web.OpenApi;

/// <summary>
/// The package's own OpenAPI document, served at /umbraco/openapi/archived-users.json (docs/spikes.md S6).
/// v18 uses Microsoft.AspNetCore.OpenApi; the v17 line registers a Swashbuckle document here instead.
/// </summary>
internal static class ArchivedUsersOpenApi
{
    public static IUmbracoBuilder AddArchivedUsersOpenApi(this IUmbracoBuilder builder)
        => builder.AddBackOfficeOpenApiDocument(
            ArchivedUsersOpenApiNames.DocumentName,
            document => document
                .WithTitle("Archived Users")
                .WithBackOfficeAuthentication()
                .WithJsonOptions(Constants.JsonOptionsNames.BackOffice));
}
