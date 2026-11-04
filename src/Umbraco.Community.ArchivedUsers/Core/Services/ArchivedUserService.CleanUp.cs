using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Security;

namespace Umbraco.Community.ArchivedUsers.Core.Services;

internal sealed partial class ArchivedUserService
{
    private const int CleanUpPageSize = 500;
    private const int CleanUpWarningThreshold = 1000;
    private const int PreviewSampleSize = 10;

    public async Task<CleanUpPreview> PreviewCleanUpAsync(Guid performerKey)
    {
        List<IUser> candidates = await GetCleanUpCandidatesAsync(performerKey);
        return new CleanUpPreview(
            candidates.Count,
            candidates.Take(PreviewSampleSize).Select(u => new UserNameItem(u.Key, u.Name ?? string.Empty)).ToList());
    }

    public async Task<IReadOnlyList<UserArchiveResult>> ArchiveAllDisabledAsync(Guid performerKey, string? reason)
    {
        List<IUser> candidates = await GetCleanUpCandidatesAsync(performerKey);
        if (candidates.Count == 0)
        {
            return [];
        }

        if (candidates.Count > CleanUpWarningThreshold)
        {
            _logger.LogWarning("Archiving {Count} disabled users in one clean-up run", candidates.Count);
        }

        // Partial success by design: each user gets its own result and audit row (docs/design.md 7.2).
        return await ArchiveAsync(performerKey, candidates.Select(u => u.Key).ToHashSet(), reason, ArchiveSource.CleanUp);
    }

    /// <summary>
    /// Disabled, non-archived users the performer may see, ordered by name. Enumerated with core's paged
    /// <c>IUserService.GetAll</c> plus <see cref="UserVisibility.CanSee"/>, not <c>FilterAsync</c>: with
    /// <c>HideDisabledUsersInBackOffice</c> on, <c>FilterAsync</c> returns no disabled users at all
    /// (docs/spikes.md S3). Never SQL on <c>umbracoUser</c>.
    /// </summary>
    private async Task<List<IUser>> GetCleanUpCandidatesAsync(Guid performerKey)
    {
        IUser? performer = await _userService.GetAsync(performerKey);
        if (!UserStateRules.CanPerform(performer))
        {
            return [];
        }

        HashSet<Guid> archived;
        using (ICoreScope scope = _scopeProvider.CreateCoreScope(autoComplete: true))
        {
            archived = await _repository.GetKeysAsync();
        }

        var candidates = new List<IUser>();
        long pageIndex = 0, total;
        do
        {
            IUser[] page = _userService
                .GetAll(pageIndex, CleanUpPageSize, out total, "Name", Direction.Ascending, [UserState.Disabled], userGroups: null)
                .ToArray();
            candidates.AddRange(page.Where(u =>
                UserStateRules.IsDisabled(u) // the state query also matches invited users; locked-out users report LockedOut
                && !archived.Contains(u.Key)
                && !UserVisibility.IsSuperUser(u.Key)
                && u.Key != performer!.Key
                && UserVisibility.CanSee(performer, u)));
            if (page.Length == 0)
            {
                break;
            }

            pageIndex++;
        }
        while (pageIndex * CleanUpPageSize < total);

        return candidates.OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
