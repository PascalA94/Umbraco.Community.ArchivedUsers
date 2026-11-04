using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Community.ArchivedUsers.Core.Models;

namespace Umbraco.Community.ArchivedUsers.Tests;

/// <summary>
/// Review C-6 and C-7: a failure for one user is reported for that user, with a status that says what happened,
/// and never fails the whole bulk call.
/// </summary>
public class Alpha2FailureStatusTests
{
    private readonly ServiceFixture _f = new();

    /// <summary>C-7: the user was disabled, but the record could not be saved. The admin must not read "Umbraco refused".</summary>
    [Fact]
    public async Task Record_failure_after_disable_returns_DisabledNotArchived()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser();
        _f.Repository.TryInsertAsync(Arg.Any<ArchivedUserRecord>()).ThrowsAsync(new InvalidOperationException("db down"));

        IReadOnlyList<UserArchiveResult> results = await _f.Create().ArchiveAsync(admin.Key, new HashSet<Guid> { target.Key }, null, ArchiveSource.Manual);

        Assert.Equal(ArchiveResultStatus.DisabledNotArchived, results[0].Status);
        Assert.Null(results[0].CoreStatus);
    }

    /// <summary>C-7: an already-disabled user whose record could not be saved is unchanged, so the status is Failed.</summary>
    [Fact]
    public async Task Record_failure_for_an_already_disabled_user_returns_Failed()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser("Disabled", UserState.Disabled);
        _f.Repository.TryInsertAsync(Arg.Any<ArchivedUserRecord>()).ThrowsAsync(new InvalidOperationException("db down"));

        IReadOnlyList<UserArchiveResult> results = await _f.Create().ArchiveAsync(admin.Key, new HashSet<Guid> { target.Key }, null, ArchiveSource.Manual);

        Assert.Equal(ArchiveResultStatus.Failed, results[0].Status);
    }

    /// <summary>C-6: a failure outside the record insert (here the record read) is that user's result, not a 500.</summary>
    [Fact]
    public async Task Bulk_archive_continues_after_an_unexpected_failure_for_one_user()
    {
        IUser admin = _f.AddAdmin();
        IUser first = _f.AddUser("First"), broken = _f.AddUser("Secret Name"), last = _f.AddUser("Last");
        Guid brokenKey = broken.Key;
        _f.Repository.GetAsync(brokenKey).ThrowsAsync(new InvalidOperationException("timeout"));

        IReadOnlyList<UserArchiveResult> results = await _f.Create().ArchiveAsync(admin.Key, new HashSet<Guid> { first.Key, brokenKey, last.Key }, "secret-reason", ArchiveSource.Bulk);

        Assert.Equal(new[] { ArchiveResultStatus.Archived, ArchiveResultStatus.Failed, ArchiveResultStatus.Archived }, results.Select(r => r.Status));
        Assert.Equal(1, _f.LogCount(LogLevel.Error));
        string log = string.Join("\n", _f.LogText());
        Assert.Contains(brokenKey.ToString(), log);
        Assert.DoesNotContain("Secret Name", log);
        Assert.DoesNotContain("secret-reason", log);
    }

    /// <summary>C-6: the same for unarchive.</summary>
    [Fact]
    public async Task Bulk_unarchive_continues_after_an_unexpected_failure_for_one_user()
    {
        IUser admin = _f.AddAdmin();
        IUser first = _f.AddUser("First", UserState.Disabled), broken = _f.AddUser("Broken", UserState.Disabled), last = _f.AddUser("Last", UserState.Disabled);
        Guid brokenKey = broken.Key;
        // Configure before the records exist: setting up a substitute call runs the fixture's fake once.
        _f.Repository.DeleteAsync(brokenKey).ThrowsAsync(new InvalidOperationException("timeout"));
        foreach (IUser u in new[] { first, broken, last })
        {
            _f.Records[u.Key] = new ArchivedUserRecord(u.Key, DateTime.UtcNow, admin.Key, ArchiveSource.Manual, null, true);
        }

        IReadOnlyList<UserArchiveResult> results = await _f.Create().UnarchiveAsync(admin.Key, new HashSet<Guid> { first.Key, brokenKey, last.Key }, null, ArchiveSource.Bulk);

        Assert.Equal(new[] { ArchiveResultStatus.Unarchived, ArchiveResultStatus.Failed, ArchiveResultStatus.Unarchived }, results.Select(r => r.Status));
        Assert.Contains(brokenKey.ToString(), string.Join("\n", _f.LogText()));
    }
}
