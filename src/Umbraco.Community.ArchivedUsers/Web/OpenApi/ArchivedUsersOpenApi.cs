#if UMBRACO_17
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
#else
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core;
#endif
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.ArchivedUsers.Web.Controllers;

namespace Umbraco.Community.ArchivedUsers.Web.OpenApi;

/// <summary>
/// The package's own OpenAPI document (docs/spikes.md S6). Umbraco 17: Swashbuckle, as core's own documents are on 17.x,
/// served at /umbraco/swagger/archived-users/swagger.json. Umbraco 18 and later: Microsoft.AspNetCore.OpenApi,
/// served at /umbraco/openapi/archived-users.json.
/// </summary>
internal static class ArchivedUsersOpenApi
{
#if UMBRACO_17
    public static IUmbracoBuilder AddArchivedUsersOpenApi(this IUmbracoBuilder builder)
    {
        builder.Services.ConfigureOptions<ArchivedUsersSwaggerGenOptions>();

        // Core's OperationIdHandler only names operations in the Umbraco.Cms.Api namespace; without this the
        // package's operations get Swashbuckle's default IDs and the generated client's function names change.
        builder.Services.AddSingleton<IOperationIdHandler, ArchivedUsersOperationIdHandler>();
        return builder;
    }
#else
    public static IUmbracoBuilder AddArchivedUsersOpenApi(this IUmbracoBuilder builder)
        => builder.AddBackOfficeOpenApiDocument(
            ArchivedUsersOpenApiNames.DocumentName,
            document => document
                .WithTitle("Archived Users")
                .WithBackOfficeAuthentication()
                .WithJsonOptions(Constants.JsonOptionsNames.BackOffice));
#endif
}

#if UMBRACO_17
/// <summary>Adds the "archived-users" document. Core's DocInclusionPredicate assigns actions by [MapToApi].</summary>
internal sealed class ArchivedUsersSwaggerGenOptions : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        options.SwaggerDoc(
            ArchivedUsersOpenApiNames.DocumentName,
            new OpenApiInfo { Title = ArchivedUsersOpenApiNames.GroupName, Version = "Latest" });

        // Core registers the backoffice OAuth security scheme once for every document; this adds the requirement
        // (and the 401/403 responses) to the package's operations, as core's filter does for the Management API.
        options.OperationFilter<ArchivedUsersSecurityRequirementsOperationFilter>();
    }
}

internal sealed class ArchivedUsersSecurityRequirementsOperationFilter : BackOfficeSecurityRequirementsOperationFilterBase
{
    protected override string ApiName => ArchivedUsersOpenApiNames.DocumentName;
}

/// <summary>Core's operation-ID naming, applied to the package's controllers only.</summary>
internal sealed class ArchivedUsersOperationIdHandler : OperationIdHandler
{
    public ArchivedUsersOperationIdHandler(IOptions<Asp.Versioning.ApiVersioningOptions> apiVersioningOptions)
        : base(apiVersioningOptions)
    {
    }

    protected override bool CanHandle(ApiDescription apiDescription, ControllerActionDescriptor controllerActionDescriptor)
        => typeof(ArchivedUsersControllerBase).IsAssignableFrom(controllerActionDescriptor.ControllerTypeInfo);
}
#endif
