using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using Umbraco.Cms.Api.Management.Controllers.User;
using Umbraco.Cms.Api.Management.Controllers.User.Filter;
using Umbraco.Community.ArchivedUsers.Web.Filters;

namespace Umbraco.Community.ArchivedUsers.Tests;

/// <summary>Review BS-5: the global gate costs nothing on other actions and fails open if the filter can't be built.</summary>
public class ArchivedUserListFilterGateTests
{
    private static (ActionExecutingContext Context, IServiceProvider Services) Context(Type controller, string action = "Filter2")
    {
        var services = Substitute.For<IServiceProvider>();
        services.GetService(Arg.Any<Type>()).Returns(_ => throw new InvalidOperationException("cannot resolve"));
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Method = "GET";
        var descriptor = new ControllerActionDescriptor
        {
            ControllerTypeInfo = controller.GetTypeInfo(),
            ActionName = action,
            AttributeRouteInfo = new AttributeRouteInfo { Template = ArchivedUserListFilter.RouteTemplate },
        };
        var context = new ActionExecutingContext(new ActionContext(http, new RouteData(), descriptor), new List<IFilterMetadata>(), new Dictionary<string, object?>(), controller: null!);
        return (context, services);
    }

    private static async Task<bool> Run(ArchivedUserListFilterGate gate, ActionExecutingContext context)
    {
        var called = false;
        await gate.OnActionExecutionAsync(context, () =>
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), controller: null!));
        });
        return called;
    }

    [Fact]
    public async Task Other_actions_never_resolve_services()
    {
        (ActionExecutingContext context, IServiceProvider services) = Context(typeof(ByKeyUserController));

        Assert.True(await Run(new ArchivedUserListFilterGate(), context));
        services.DidNotReceiveWithAnyArgs().GetService(default!);
    }

    [Fact]
    public async Task Resolution_failure_on_the_user_list_fails_open()
    {
        (ActionExecutingContext context, _) = Context(typeof(FilterUserFilterController));

        Assert.True(await Run(new ArchivedUserListFilterGate(), context));
        Assert.Null(context.Result);
    }
}
