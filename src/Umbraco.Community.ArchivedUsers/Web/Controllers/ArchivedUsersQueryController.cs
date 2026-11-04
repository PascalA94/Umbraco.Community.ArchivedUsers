using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Common.ViewModels.Pagination;
using Umbraco.Cms.Api.Management.Factories;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Community.ArchivedUsers.Core.Configuration;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Services;
using Umbraco.Community.ArchivedUsers.Web.Controllers.Models;

namespace Umbraco.Community.ArchivedUsers.Web.Controllers;

/// <summary>Read routes. None of them changes state (AU-14), including for stale records (AU-03).</summary>
[ApiVersion("1.0")]
public sealed class ArchivedUsersQueryController : ArchivedUsersControllerBase
{
    private const int MaxTake = 1000;

    private readonly IArchivedUserService _service;
    private readonly IBackOfficeSecurityAccessor _security;
    private readonly IUserPresentationFactory _presentation;
    private readonly IOptionsMonitor<ArchivedUsersSettings> _settings;

    public ArchivedUsersQueryController(
        IArchivedUserService service,
        IBackOfficeSecurityAccessor security,
        IUserPresentationFactory presentation,
        IOptionsMonitor<ArchivedUsersSettings> settings)
    {
        _service = service;
        _security = security;
        _presentation = presentation;
        _settings = settings;
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(PagedViewModel<ArchivedUserResponseModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [EndpointSummary("Gets a paged list of archived users the current user may see.")]
    public async Task<IActionResult> GetArchivedUsers(
        int skip = 0,
        int take = 100,
        string? filter = null,
        ArchivedUserOrder orderBy = ArchivedUserOrder.ArchivedUtc,
        Direction orderDirection = Direction.Descending)
    {
        // P14: bounded paging. An unknown orderBy fails enum binding and the ApiController filter returns 400.
        if (skip < 0 || take < 0 || take > MaxTake)
        {
            return Problem400("Invalid paging", $"skip must be 0 or more and take between 0 and {MaxTake}.");
        }

        PagedModel<ArchivedUserView> page = await _service.GetArchivedAsync(CurrentUserKey(_security), skip, take, filter, orderBy, orderDirection);
        return Ok(new PagedViewModel<ArchivedUserResponseModel> { Total = page.Total, Items = page.Items.Select(ToModel).ToArray() });
    }

    [HttpGet("{id:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(ArchivedUserResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [EndpointSummary("Gets the archive status of one user.")]
    public async Task<IActionResult> GetArchivedUser(Guid id)
    {
        ArchivedUserView? view = await _service.GetArchivedUserAsync(CurrentUserKey(_security), id);
        return view is null ? NotFound() : Ok(ToModel(view));
    }

    [HttpGet("clean-up/preview")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(CleanUpPreviewResponseModel), StatusCodes.Status200OK)]
    [EndpointSummary("Counts the disabled users that Archive disabled users would archive.")]
    public async Task<IActionResult> PreviewCleanUp()
    {
        CleanUpPreview preview = await _service.PreviewCleanUpAsync(CurrentUserKey(_security));
        return Ok(new CleanUpPreviewResponseModel
        {
            Count = preview.Count,
            Sample = preview.Sample.Select(s => new UserNameResponseModel { Id = s.Id, Name = s.Name }).ToArray(),
        });
    }

    [HttpGet("config")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(ArchivedUsersConfigResponseModel), StatusCodes.Status200OK)]
    [EndpointSummary("Gets the package settings the backoffice needs.")]
    public IActionResult GetConfig()
    {
        ArchivedUsersSettings settings = _settings.CurrentValue;
        return Ok(new ArchivedUsersConfigResponseModel
        {
            HideArchivedFromUserList = settings.HideArchivedFromUserList,
            RequireReason = settings.RequireReason,
            UnarchiveEnablesUser = settings.UnarchiveEnablesUser,
            AdminGroupId = Constants.Security.AdminGroupKey,
            SuperUserId = Constants.Security.SuperUserKey,
        });
    }

    private ArchivedUserResponseModel ToModel(ArchivedUserView view) => new()
    {
        User = _presentation.CreateResponseModel(view.User),
        ArchivedUtc = DateTime.SpecifyKind(view.Record.ArchivedUtc, DateTimeKind.Utc),
        ArchivedBy = view.Record.ArchivedByUserKey is { } by ? new ArchivedByResponseModel { Id = by, Name = view.ArchivedByName } : null,
        Reason = view.Record.Reason,
        Source = view.Record.Source.ToString(),
        Stale = view.IsStale,
    };
}
