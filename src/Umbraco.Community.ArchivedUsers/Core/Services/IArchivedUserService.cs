using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Models;

namespace Umbraco.Community.ArchivedUsers.Core.Services;

/// <summary>
/// The single choke point for every archive write (docs/design.md 4, AU-01). <c>IUserService</c> does not
/// authorise, so every method here checks, per target user, core's permission rule and the package's
/// visibility rule before anything changes.
/// </summary>
public interface IArchivedUserService
{
    /// <summary>Archives each user (disable, record, audit). <paramref name="reason"/> is already normalised by the caller.</summary>
    Task<IReadOnlyList<UserArchiveResult>> ArchiveAsync(Guid performerKey, ISet<Guid> userKeys, string? reason, ArchiveSource source);

    /// <summary>Counts the disabled, non-archived users the performer may see, with up to ten sample names (F3).</summary>
    Task<CleanUpPreview> PreviewCleanUpAsync(Guid performerKey);

    /// <summary>Archives every user the preview counts, with source <see cref="ArchiveSource.CleanUp"/> (F3).</summary>
    Task<IReadOnlyList<UserArchiveResult>> ArchiveAllDisabledAsync(Guid performerKey, string? reason);

    /// <summary>
    /// Removes each user's archive record. The user stays disabled (D3) unless the site setting
    /// <c>UnarchiveEnablesUser</c> is on and the user was enabled before archiving; <paramref name="enable"/> can only
    /// narrow that (AU-11). Callers reject <c>enable: true</c> with the setting off before calling.
    /// </summary>
    Task<IReadOnlyList<UserArchiveResult>> UnarchiveAsync(Guid performerKey, ISet<Guid> userKeys, bool? enable, ArchiveSource source);

    /// <summary>The Archived view: records the performer may see, searched, sorted and paged in memory. Read-only (AU-14).</summary>
    Task<PagedModel<ArchivedUserView>> GetArchivedAsync(Guid performerKey, int skip, int take, string? filter, ArchivedUserOrder orderBy, Direction direction);

    /// <summary>One archived user, or null when not archived or not visible to the performer.</summary>
    Task<ArchivedUserView?> GetArchivedUserAsync(Guid performerKey, Guid userKey);

    Task<HashSet<Guid>> GetArchivedKeysAsync();

    Task<bool> IsArchivedAsync(Guid userKey);
}
