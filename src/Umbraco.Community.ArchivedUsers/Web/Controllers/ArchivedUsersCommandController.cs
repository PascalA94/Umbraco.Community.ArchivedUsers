using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Security;
using Umbraco.Community.ArchivedUsers.Core.Configuration;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Services;
using Umbraco.Community.ArchivedUsers.Web.Controllers.Models;

namespace Umbraco.Community.ArchivedUsers.Web.Controllers;

/// <summary>
/// Write routes. JSON bodies only: every action binds [FromBody] and only the JSON input formatter is registered
/// for the backoffice, so a form post gets 415 (AU-14). [Consumes] is not used: with Umbraco's endpoint routing it
/// turns a wrong content type into a 404 instead of a 415. The performer is always the authenticated user.
/// Authorisation is per user inside the service, not a batch check here, so a mixed bulk selection still gets
/// per-user results (docs/design.md "Security review changes", AU-01).
/// </summary>
[ApiVersion("1.0")]
public sealed class ArchivedUsersCommandController : ArchivedUsersControllerBase
{
    private const int MaxUsersPerRequest = 500;

    private readonly IArchivedUserService _service;
    private readonly IBackOfficeSecurityAccessor _security;
    private readonly IOptionsMonitor<ArchivedUsersSettings> _settings;

    public ArchivedUsersCommandController(IArchivedUserService service, IBackOfficeSecurityAccessor security, IOptionsMonitor<ArchivedUsersSettings> settings)
    {
        _service = service;
        _security = security;
        _settings = settings;
    }

    [HttpPost("archive")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(UserArchiveResultResponseModel[]), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [EndpointSummary("Archives users: disables them, records who, when and why, and hides them from the Users list.")]
    public async Task<IActionResult> Archive([FromBody] ArchiveUsersRequestModel model)
    {
        if (ValidateIds(model.UserIds) is { } idError)
        {
            return idError;
        }

        Attempt<string?, ReasonError> reason = ReasonValidator.Normalise(model.Reason, _settings.CurrentValue.RequireReason);
        if (!reason.Success)
        {
            return ReasonProblem(reason.Status);
        }

        HashSet<Guid> keys = model.UserIds.ToHashSet();
        IReadOnlyList<UserArchiveResult> results = await _service.ArchiveAsync(CurrentUserKey(_security), keys, reason.Result, SourceFor(keys));
        return Ok(results.Select(ToResponse).ToArray());
    }

    [HttpPost("unarchive")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(UserArchiveResultResponseModel[]), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [EndpointSummary("Unarchives users. They stay disabled unless the site is configured to restore their earlier state.")]
    public async Task<IActionResult> Unarchive([FromBody] UnarchiveUsersRequestModel model)
    {
        if (ValidateIds(model.UserIds) is { } idError)
        {
            return idError;
        }

        // AU-11: a request can narrow the site policy, never widen it. Re-enabling stays core's Enable action.
        if (model.Enable == true && !_settings.CurrentValue.UnarchiveEnablesUser)
        {
            return Problem400("Unarchive cannot enable users on this site", "UnarchiveEnablesUser is off. Enable the user with core's Enable action.");
        }

        HashSet<Guid> keys = model.UserIds.ToHashSet();
        IReadOnlyList<UserArchiveResult> results = await _service.UnarchiveAsync(CurrentUserKey(_security), keys, model.Enable, SourceFor(keys));
        return Ok(results.Select(ToResponse).ToArray());
    }

    [HttpPost("clean-up")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(UserArchiveResultResponseModel[]), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [EndpointSummary("Archives every disabled user the current user may see.")]
    public async Task<IActionResult> CleanUp([FromBody] CleanUpRequestModel model)
    {
        Attempt<string?, ReasonError> reason = ReasonValidator.Normalise(model.Reason, _settings.CurrentValue.RequireReason);
        if (!reason.Success)
        {
            return ReasonProblem(reason.Status);
        }

        IReadOnlyList<UserArchiveResult> results = await _service.ArchiveAllDisabledAsync(CurrentUserKey(_security), reason.Result);
        return Ok(results.Select(ToResponse).ToArray());
    }

    private static ArchiveSource SourceFor(ISet<Guid> keys) => keys.Count == 1 ? ArchiveSource.Manual : ArchiveSource.Bulk;

    private IActionResult? ValidateIds(Guid[]? ids)
    {
        if (ids is null || ids.Length == 0)
        {
            return Problem400("No users", "userIds must contain at least one id.");
        }

        return ids.Distinct().Count() > MaxUsersPerRequest
            ? Problem400("Too many users", $"At most {MaxUsersPerRequest} users per request.")
            : null;
    }

    private IActionResult ReasonProblem(ReasonError error) => error == ReasonError.Required
        ? Problem400("A reason is required", "This site requires a reason for archiving.")
        : Problem400("Reason too long", $"The reason can be at most {ReasonValidator.MaxLength} characters.");
}
