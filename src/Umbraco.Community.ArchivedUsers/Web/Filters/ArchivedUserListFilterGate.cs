using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Umbraco.Community.ArchivedUsers.Web.Filters;

/// <summary>
/// The globally registered part of the list filter. It has no dependencies, so the rest of the site's MVC actions
/// cost one type check. Only for core's <c>GET filter/user</c> does it resolve <see cref="ArchivedUserListFilter"/>,
/// inside a try, so even a DI failure falls back to core's response (AU-10, milestone review B, BS-5).
/// </summary>
public sealed class ArchivedUserListFilterGate : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!ArchivedUserListFilter.IsCoreUserFilter(context))
        {
            await next();
            return;
        }

        ArchivedUserListFilter filter;
        try
        {
            filter = context.HttpContext.RequestServices.GetRequiredService<ArchivedUserListFilter>();
        }
        catch (Exception)
        {
            // Nothing can be logged safely without services; core answers as if the package weren't installed.
            await next();
            return;
        }

        await filter.OnActionExecutionAsync(context, next);
    }
}
