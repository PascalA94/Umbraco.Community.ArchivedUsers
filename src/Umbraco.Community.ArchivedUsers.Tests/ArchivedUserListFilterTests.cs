using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Umbraco.Cms.Api.Common.ViewModels.Pagination;
using Umbraco.Cms.Api.Management.Controllers.User;
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
using Umbraco.Community.ArchivedUsers.Web.Filters;

namespace Umbraco.Community.ArchivedUsers.Tests;

public class ArchivedUserListFilterTests
{
    private readonly ArchivedUsersSettings _settings = new();
    private readonly IArchivedUserService _archive = Substitute.For<IArchivedUserService>();
    private readonly IUserService _users = Substitute.For<IUserService>();
    private readonly IUserPresentationFactory _presentation = Substitute.For<IUserPresentationFactory>();
    private readonly IBackOfficeSecurityAccessor _security = Substitute.For<IBackOfficeSecurityAccessor>();
    private readonly FakeLogger<ArchivedUserListFilter> _logger = new();
    private readonly IUser _viewer = Substitute.For<IUser>();
    private readonly ArchivedUserListWarningClock _clock = new(); // a DI singleton in production
    private readonly List<IUser> _all = new();
    private bool _nextCalled;

    public ArchivedUserListFilterTests()
    {
        _viewer.Key.Returns(Guid.NewGuid());
        _viewer.Kind.Returns(UserKind.Default);
        var backOffice = Substitute.For<IBackOfficeSecurity>();
        backOffice.CurrentUser.Returns(_viewer);
        _security.BackOfficeSecurity.Returns(backOffice);
        _presentation.CreateResponseModel(Arg.Any<IUser>()).Returns(ci => new UserResponseModel { Id = ci.Arg<IUser>().Key });

        for (var i = 0; i < 5; i++)
        {
            var user = Substitute.For<IUser>();
            user.Key.Returns(Guid.NewGuid());
            user.UserState.Returns(i == 2 ? UserState.Disabled : UserState.Active);
            _all.Add(user);
        }

        Guid archivedKey = _all[2].Key; // read before Returns: NSubstitute can't nest substitute calls
        _archive.GetArchivedKeysAsync().Returns(new HashSet<Guid> { archivedKey });
        _users.FilterAsync(default, default!, default, default, default, default).ReturnsForAnyArgs(
            Attempt.SucceedWithStatus(UserOperationStatus.Success, new PagedModel<IUser>(_all.Count, _all)));
    }

    private ArchivedUserListFilter Create() => new(
        ServiceFixture.Options(_settings), _archive, _users, _presentation, _security, _clock, new FakeTimeProvider(), _logger);

    private ActionExecutingContext Context(Type? controller = null, string action = "Filter", string method = "GET")
    {
        var http = new DefaultHttpContext();
        http.Request.Method = method;
        var descriptor = new ControllerActionDescriptor
        {
            ControllerTypeInfo = (controller ?? typeof(FilterUserFilterController)).GetTypeInfo(),
            ActionName = action,
            AttributeRouteInfo = new AttributeRouteInfo { Template = "umbraco/management/api/v{version:apiVersion}/filter/user" },
        };
        var actionContext = new ActionContext(http, new RouteData(), descriptor);
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?> { ["skip"] = 0, ["take"] = 100 }, controller: null!);
    }

    private async Task<ActionExecutingContext> Run(ActionExecutingContext context)
    {
        await Create().OnActionExecutionAsync(context, () =>
        {
            _nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), controller: null!));
        });
        return context;
    }

    private static PagedViewModel<UserResponseModel> Payload(ActionExecutingContext context)
        => (PagedViewModel<UserResponseModel>)((OkObjectResult)context.Result!).Value!;

    [Fact]
    public async Task Archived_and_disabled_user_is_dropped_with_the_filtered_total()
    {
        ActionExecutingContext context = await Run(Context());

        Assert.False(_nextCalled);
        PagedViewModel<UserResponseModel> payload = Payload(context);
        Assert.Equal(4, payload.Total);
        Assert.DoesNotContain(payload.Items, i => i.Id == _all[2].Key);
    }

    [Fact]
    public async Task Setting_off_calls_next_and_leaves_result_null()
    {
        _settings.HideArchivedFromUserList = false;

        ActionExecutingContext context = await Run(Context());

        Assert.True(_nextCalled);
        Assert.Null(context.Result);
        await _users.DidNotReceiveWithAnyArgs().FilterAsync(default, default!, default, default, default, default);
    }

    [Fact]
    public async Task No_archived_users_calls_next()
    {
        _archive.GetArchivedKeysAsync().Returns(new HashSet<Guid>());

        await Run(Context());

        Assert.True(_nextCalled);
        await _users.DidNotReceiveWithAnyArgs().FilterAsync(default, default!, default, default, default, default);
    }

    [Fact]
    public async Task Other_controller_calls_next()
    {
        await Run(Context(typeof(ByKeyUserController)));

        Assert.True(_nextCalled);
        await _archive.DidNotReceive().GetArchivedKeysAsync();
    }

    [Fact]
    public async Task Same_controller_other_action_calls_next()
    {
        // v17 line: the bound action is Filter, so v18's Filter2 name must not match.
        await Run(Context(action: "Filter2"));

        Assert.True(_nextCalled);
    }

    /// <summary>AU-10, T9.</summary>
    [Fact]
    public async Task Non_GET_method_calls_next()
    {
        await Run(Context(method: "POST"));

        Assert.True(_nextCalled);
    }

#if UMBRACO_HAS_API_USERS
    /// <summary>AU-10: client-credential integrations get core's list by default.</summary>
    [Fact]
    public async Task Api_user_with_HideArchivedForApiUsers_false_calls_next()
    {
        _viewer.Kind.Returns(UserKind.Api);

        await Run(Context());

        Assert.True(_nextCalled);
    }

    [Fact]
    public async Task Api_user_with_HideArchivedForApiUsers_true_filters()
    {
        _viewer.Kind.Returns(UserKind.Api);
        _settings.HideArchivedForApiUsers = true;

        ActionExecutingContext context = await Run(Context());

        Assert.False(_nextCalled);
        Assert.Equal(4, Payload(context).Total);
    }
#endif

    [Fact]
    public async Task Core_FilterAsync_failure_calls_next()
    {
        _users.FilterAsync(default, default!, default, default, default, default).ReturnsForAnyArgs(
            Attempt.FailWithStatus(UserOperationStatus.MissingUser, new PagedModel<IUser>()));

        ActionExecutingContext context = await Run(Context());

        Assert.True(_nextCalled);
        Assert.Null(context.Result);
    }

    /// <summary>AU-10, T8: hiding is a convenience, not a control; any failure returns core's response.</summary>
    [Fact]
    public async Task Repository_exception_calls_next_and_logs_once()
    {
        _archive.GetArchivedKeysAsync().ThrowsAsync(new InvalidOperationException("no such table"));

        await Run(Context());
        await Run(Context());

        Assert.True(_nextCalled);
        Assert.Equal(1, _logger.Collector.GetSnapshot().Count(r => r.Level == LogLevel.Error));
    }

    [Fact]
    public async Task Presentation_factory_exception_calls_next()
    {
        _presentation.CreateResponseModel(Arg.Any<IUser>()).Throws(new InvalidOperationException("mapping"));

        ActionExecutingContext context = await Run(Context());

        Assert.True(_nextCalled);
        Assert.Null(context.Result);
    }

    /// <summary>AU-13: no unbounded loads; above the ceiling the site gets core's list.</summary>
    [Fact]
    public async Task Total_above_20000_calls_next_and_warns()
    {
        _users.FilterAsync(default, default!, default, default, default, default).ReturnsForAnyArgs(
            Attempt.SucceedWithStatus(UserOperationStatus.Success, new PagedModel<IUser>(20001, _all)));

        await Run(Context());

        Assert.True(_nextCalled);
        Assert.Equal(1, _logger.Collector.GetSnapshot().Count(r => r.Level == LogLevel.Warning));
    }

    [Fact]
    public void Warning_clock_allows_one_warning_per_hour_per_key()
    {
        var clock = new ArchivedUserListWarningClock();
        var now = DateTimeOffset.UtcNow;

        Assert.True(clock.ShouldWarn("a", now));
        Assert.False(clock.ShouldWarn("a", now.AddMinutes(59)));
        Assert.True(clock.ShouldWarn("b", now));
        Assert.True(clock.ShouldWarn("a", now.AddMinutes(61)));
    }
}
