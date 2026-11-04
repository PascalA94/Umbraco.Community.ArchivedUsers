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
}
