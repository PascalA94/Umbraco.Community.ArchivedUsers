using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.ArchivedUsers.Web.Controllers;

namespace Umbraco.Community.ArchivedUsers.Web.OpenApi;

/// <summary>
/// The package's own OpenAPI document, served at /umbraco/swagger/archived-users/swagger.json (docs/spikes.md S6).
/// v17 line: Swashbuckle, as core's own documents are on 17.x. v18 uses Microsoft.AspNetCore.OpenApi instead.
/// </summary>
internal static class ArchivedUsersOpenApi
{
    public static IUmbracoBuilder AddArchivedUsersOpenApi(this IUmbracoBuilder builder)
    {
        builder.Services.ConfigureOptions<ArchivedUsersSwaggerGenOptions>();

        // Core's OperationIdHandler only names operations in the Umbraco.Cms.Api namespace; without this the
        // package's operations get Swashbuckle's default IDs and the generated client's function names change.
        builder.Services.AddSingleton<IOperationIdHandler, ArchivedUsersOperationIdHandler>();
        return builder;
    }
}

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
