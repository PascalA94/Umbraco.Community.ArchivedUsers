using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Common.Builders;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Web.Controllers.Models;

namespace Umbraco.Community.ArchivedUsers.Web.Controllers;

/// <summary>
/// S1 [Critical]: every package route is a Management API route behind the Users-section policy. The service
/// authorises each user again (AU-01), so this policy is the outer gate, not the only one.
/// </summary>
[VersionedApiBackOfficeRoute(RouteSegment)]
[Authorize(Policy = AuthorizationPolicies.SectionAccessUsers)]
[ApiExplorerSettings(GroupName = ArchivedUsersOpenApiNames.GroupName)]
[MapToApi(ArchivedUsersOpenApiNames.DocumentName)]
public abstract class ArchivedUsersControllerBase : ManagementApiControllerBase
{
    public const string RouteSegment = "archived-users";

    protected IActionResult Problem400(string title, string? detail = null)
        => BadRequest(new ProblemDetailsBuilder().WithTitle(title).WithDetail(detail ?? title).Build());

    protected static UserArchiveResultResponseModel ToResponse(UserArchiveResult result) => new()
    {
        Id = result.Id,
        Status = result.Status == ArchiveResultStatus.CoreRefused && result.CoreStatus is { } core
            ? $"{nameof(ArchiveResultStatus.CoreRefused)}:{core}"
            : result.Status.ToString(),
        Message = result.Message,
    };
}

public static class ArchivedUsersOpenApiNames
{
    public const string DocumentName = "archived-users";

    public const string GroupName = "Archived Users";
}
