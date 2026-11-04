using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Community.ArchivedUsers.Core.Security;

namespace Umbraco.Community.ArchivedUsers.Web.Filters;

/// <summary>In-memory drop-and-page over core's ordered list (docs/design.md 7.2, list filter steps 3 and 4).</summary>
public static class UserListPaging
{
    /// <summary>Drops <paramref name="drop"/>, keeps core's order, and pages; the total is the filtered count.</summary>
    public static (IReadOnlyList<T> Page, int Total) Apply<T>(IReadOnlyList<T> ordered, Func<T, Guid> key, ISet<Guid> drop, int skip, int take)
    {
        List<T> kept = ordered.Where(item => !drop.Contains(key(item))).ToList();
        return (kept.Skip(Math.Max(skip, 0)).Take(Math.Max(take, 0)).ToList(), kept.Count);
    }

    /// <summary>
    /// Keys to hide: archived <b>and</b> disabled. An archived key whose user is enabled (a stale record) is kept,
    /// so an enabled account is never hidden (AU-03).
    /// </summary>
    public static HashSet<Guid> DropSet(IEnumerable<IUser> users, ISet<Guid> archivedKeys)
        => users.Where(u => archivedKeys.Contains(u.Key) && UserStateRules.IsDisabled(u)).Select(u => u.Key).ToHashSet();
}
