using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;

namespace Umbraco.Community.ArchivedUsers.Core.Security;

/// <summary>
/// Who may see whom. Pure functions, so a static class is safe (no scoped state).
/// </summary>
/// <remarks>
/// Mirrors core's <c>UserService.CreateBaseUserFilter</c> (Umbraco 18.3.0, UserService.cs lines 1461-1484):
/// only the superuser sees the superuser, and only admins see admins. Re-check it on each minor;
/// <c>CoreContractTests.FilterAsync_hides_admins_from_non_admin</c> is the tripwire.
/// This is not a substitute for core's <c>IUserPermissionService.AuthorizeAccessAsync</c>, which the service
/// also calls: it adds the superuser rule that core's permission policy lacks (docs/design.md section 4, AU-01).
/// </remarks>
public static class UserVisibility
{
    public static bool CanSee(IUser performer, IUser target)
    {
        if (IsSuperUser(target.Key) && !IsSuperUser(performer.Key))
        {
            return false;
        }

        return !IsAdmin(target) || IsAdmin(performer);
    }

    public static bool IsAdmin(IUser user)
        => user.Groups.Any(g => string.Equals(g.Alias, Constants.Security.AdminGroupAlias, StringComparison.Ordinal));

    public static bool IsSuperUser(Guid key) => key == Constants.Security.SuperUserKey;
}
