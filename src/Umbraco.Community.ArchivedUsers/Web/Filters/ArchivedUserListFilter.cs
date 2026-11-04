using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Common.ViewModels.Pagination;
using Umbraco.Cms.Api.Management.Controllers.User.Filter;
using Umbraco.Cms.Api.Management.Factories;
using Umbraco.Cms.Api.Management.ViewModels.User;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Community.ArchivedUsers.Core.Configuration;
using Umbraco.Community.ArchivedUsers.Core.Services;

namespace Umbraco.Community.ArchivedUsers.Web.Filters;

/// <summary>
/// F6 / D1: hides archived and disabled users from core's <c>GET filter/user</c>, which backs the Users collection,
/// the user group member list and every user picker (docs/spikes.md S1, U5). Hiding is a convenience, not access
/// control: anything unexpected falls back to core's own response (AU-10).
/// </summary>
/// <remarks>
/// v17 line: core's action is <c>Filter</c> (no <c>userKinds</c> parameter), FilterUserFilterController at
/// release-17.8.0, commit 83984e2. If core renames the action, this stops hiding, which is core behaviour, not a leak;
/// <c>CoreContractTests.Filter_user_action_name_is_Filter</c> fails loudly when that happens.
/// </remarks>
public sealed class ArchivedUserListFilter : IAsyncActionFilter
{
    public const string BoundActionName = "Filter";
    public const string RouteTemplate = "umbraco/management/api/v{version:apiVersion}/filter/user";

    /// <summary>AU-13: above this many visible users the filter steps aside rather than load them all.</summary>
    public const int MaxUsers = 20000;

    private const int LargeListWarning = 5000;

    private readonly IOptionsMonitor<ArchivedUsersSettings> _settings;
    private readonly IArchivedUserService _archive;
    private readonly IUserService _userService;
    private readonly IUserPresentationFactory _presentation;
    private readonly IBackOfficeSecurityAccessor _security;
    private readonly ArchivedUserListWarningClock _clock;
    private readonly TimeProvider _time;
    private readonly ILogger<ArchivedUserListFilter> _logger;

    public ArchivedUserListFilter(
        IOptionsMonitor<ArchivedUsersSettings> settings,
        IArchivedUserService archive,
        IUserService userService,
        IUserPresentationFactory presentation,
        IBackOfficeSecurityAccessor security,
        ArchivedUserListWarningClock clock,
        TimeProvider time,
        ILogger<ArchivedUserListFilter> logger)
    {
        _settings = settings;
        _archive = archive;
        _userService = userService;
        _presentation = presentation;
        _security = security;
        _clock = clock;
        _time = time;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!IsCoreUserFilter(context) || !_settings.CurrentValue.HideArchivedFromUserList)
        {
            await next();
            return;
        }

        IActionResult? result = null;
        try
        {
            result = await BuildResultAsync(context);
        }
        catch (Exception ex)
        {
            // Fail open (AU-10): log at most once an hour, never with user data, and let core answer.
            if (_clock.ShouldWarn("error", _time.GetUtcNow()))
            {
                _logger.LogError(ex, "Archived users list filter failed; core's user list was returned instead");
            }
        }

        if (result is null)
        {
            await next();
            return;
        }

        context.Result = result;
    }

    /// <summary>AU-10: only core's filter controller, only the line's bound action, only its route, only GET.</summary>
    internal static bool IsCoreUserFilter(ActionExecutingContext context)
        => context.ActionDescriptor is ControllerActionDescriptor action
           && action.ControllerTypeInfo.AsType() == typeof(FilterUserFilterController)
           && action.ActionName == BoundActionName
           && string.Equals(action.AttributeRouteInfo?.Template, RouteTemplate, StringComparison.OrdinalIgnoreCase)
           && HttpMethods.IsGet(context.HttpContext.Request.Method);

    /// <summary>Returns the filtered payload, or null to let core answer.</summary>
    private async Task<IActionResult?> BuildResultAsync(ActionExecutingContext context)
    {
        IUser? viewer = _security.BackOfficeSecurity?.CurrentUser;
        if (viewer is null)
        {
            return null;
        }

#if UMBRACO_HAS_API_USERS
        // AU-10: client-credential integrations get core's list unless the site opts in.
        if (viewer.Kind == UserKind.Api && !_settings.CurrentValue.HideArchivedForApiUsers)
        {
            return null;
        }
#endif

        HashSet<Guid> archivedKeys = await _archive.GetArchivedKeysAsync();
        if (archivedKeys.Count == 0)
        {
            return null;
        }

        // Model binding has already run, so the arguments are core's own. Parameters the request did not supply are
        // absent from ActionArguments; apply core's defaults (Filter at release-17.8.0, commit 83984e2; docs/spikes.md S1).
        IDictionary<string, object?> args = context.ActionArguments;
        int skip = Arg(args, "skip", 0);
        int take = Arg(args, "take", 100);
        UserOrder orderBy = Arg(args, "orderBy", UserOrder.UserName);
        Direction direction = Arg(args, "orderDirection", Direction.Ascending);
        string? text = Arg<string?>(args, "filter", null);
        var filter = new UserFilter
        {
            IncludedUserGroups = Arg<HashSet<Guid>?>(args, "userGroupIds", null),
            IncludeUserStates = Arg<HashSet<UserState>?>(args, "userStates", null),
            NameFilters = string.IsNullOrEmpty(text) ? null : new HashSet<string> { text },
        };

        // Core's visibility rules (admins, superuser, HideDisabledUsersInBackOffice) still apply: same call, same performer.
        Attempt<PagedModel<IUser>, UserOperationStatus> attempt = await _userService.FilterAsync(viewer.Key, filter, 0, MaxUsers, orderBy, direction);
        if (!attempt.Success)
        {
            return null;
        }

        if (attempt.Result.Total > MaxUsers)
        {
            if (_clock.ShouldWarn("ceiling", _time.GetUtcNow()))
            {
                _logger.LogWarning("More than {Max} users match the Users list; archived users are not hidden", MaxUsers);
            }

            return null;
        }

        if (attempt.Result.Total > LargeListWarning && _clock.ShouldWarn("large", _time.GetUtcNow()))
        {
            _logger.LogWarning("{Total} users match the Users list; hiding archived users pages them in memory", attempt.Result.Total);
        }

        List<IUser> ordered = attempt.Result.Items.ToList();
        HashSet<Guid> drop = UserListPaging.DropSet(ordered, archivedKeys);
        (IReadOnlyList<IUser> page, int total) = UserListPaging.Apply(ordered, u => u.Key, drop, skip, take);

        return new OkObjectResult(new PagedViewModel<UserResponseModel>
        {
            Total = total,
            Items = page.Select(_presentation.CreateResponseModel).ToArray(),
        });
    }

    private static T Arg<T>(IDictionary<string, object?> args, string name, T fallback)
        => args.TryGetValue(name, out object? value) && value is T typed ? typed : fallback;
}
