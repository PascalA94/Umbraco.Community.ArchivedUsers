using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Notifications;

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

    /// <summary>A2-9: a failed user's lock is released and no Archived notification is published for them.</summary>
    [Fact]
    public async Task Failed_user_releases_the_lock_and_publishes_nothing()
    {
        IUser admin = _f.AddAdmin(), broken = _f.AddUser("Broken");
        Guid brokenKey = broken.Key;
        _f.Repository.GetAsync(brokenKey).ThrowsAsync(new InvalidOperationException("timeout"));

        await _f.Create().ArchiveAsync(admin.Key, new HashSet<Guid> { brokenKey }, null, ArchiveSource.Manual);

        Assert.Contains($"release:{brokenKey}", _f.Locks.Events);
        await _f.Events.DidNotReceive().PublishAsync(Arg.Any<UserArchivedNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A2-2: logs carry the key and the exception type only. Exception text can hold SQL parameters.</summary>
    [Fact]
    public async Task Failure_logs_never_contain_exception_text()
    {
        IUser admin = _f.AddAdmin(), first = _f.AddUser("First"), second = _f.AddUser("Second", UserState.Disabled);
        Guid firstKey = first.Key, secondKey = second.Key;
        _f.Repository.GetAsync(firstKey).ThrowsAsync(new InvalidOperationException("param: Secret Name secret-reason"));
        _f.Repository.TryInsertAsync(Arg.Is<ArchivedUserRecord>(r => r.UserKey == secondKey)).ThrowsAsync(new InvalidOperationException("param: Secret Name secret-reason"));

        await _f.Create().ArchiveAsync(admin.Key, new HashSet<Guid> { firstKey, secondKey }, null, ArchiveSource.Bulk);

        string log = string.Join("\n", _f.LogText());
        Assert.DoesNotContain("Secret Name", log);
        Assert.DoesNotContain("secret-reason", log);
        Assert.Contains(nameof(InvalidOperationException), log);
        Assert.Contains(firstKey.ToString(), log);
    }

    /// <summary>A2-3: a throwing UserArchived subscriber is that subscriber's problem; the user is archived and the batch goes on.</summary>
    [Fact]
    public async Task Throwing_archived_subscriber_keeps_results_and_finishes_the_batch()
    {
        IUser admin = _f.AddAdmin(), first = _f.AddUser("First"), second = _f.AddUser("Second"), third = _f.AddUser("Third");
        Guid secondKey = second.Key;
        _f.Events.PublishAsync(Arg.Is<UserArchivedNotification>(n => n.Event.UserKey == secondKey), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("webhook down"));

        IReadOnlyList<UserArchiveResult> results = await _f.Create().ArchiveAsync(admin.Key, new HashSet<Guid> { first.Key, secondKey, third.Key }, null, ArchiveSource.Bulk);

        Assert.All(results, r => Assert.Equal(ArchiveResultStatus.Archived, r.Status));
        Assert.Equal(3, _f.Records.Count);
    }

    /// <summary>A2-3: the same for unarchive.</summary>
    [Fact]
    public async Task Throwing_unarchived_subscriber_keeps_results_and_finishes_the_batch()
    {
        IUser admin = _f.AddAdmin();
        IUser first = _f.AddUser("First", UserState.Disabled), second = _f.AddUser("Second", UserState.Disabled), third = _f.AddUser("Third", UserState.Disabled);
        Guid secondKey = second.Key;
        _f.Events.PublishAsync(Arg.Is<UserUnarchivedNotification>(n => n.Event.UserKey == secondKey), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("webhook down"));
        foreach (IUser u in new[] { first, second, third })
        {
            _f.Records[u.Key] = new ArchivedUserRecord(u.Key, DateTime.UtcNow, admin.Key, ArchiveSource.Manual, null, true);
        }

        IReadOnlyList<UserArchiveResult> results = await _f.Create().UnarchiveAsync(admin.Key, new HashSet<Guid> { first.Key, secondKey, third.Key }, null, ArchiveSource.Bulk);

        Assert.All(results, r => Assert.Equal(ArchiveResultStatus.Unarchived, r.Status));
        Assert.Empty(_f.Records);
    }

    /// <summary>
    /// A2-1: core disabled the user and then threw (for example a third-party UserSaved handler after the commit).
    /// The result must say what is true now, not "try again" for a change that happened.
    /// </summary>
    [Fact]
    public async Task Disable_that_throws_after_disabling_reports_DisabledNotArchived()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser("Target");
        Guid key = target.Key;
        _f.UserService.When(s => s.DisableAsync(Arg.Any<Guid>(), Arg.Is<ISet<Guid>>(set => set.Contains(key)))).Do(_ =>
        {
            target.UserState.Returns(UserState.Disabled);
            target.IsApproved.Returns(false);
            throw new InvalidOperationException("post-commit handler failed");
        });

        IReadOnlyList<UserArchiveResult> results = await _f.Create().ArchiveAsync(admin.Key, new HashSet<Guid> { key }, null, ArchiveSource.Manual);

        Assert.Equal(ArchiveResultStatus.DisabledNotArchived, results[0].Status);
    }

    /// <summary>A2-1: a failure after the record was removed reports Unarchived, which is what is true.</summary>
    [Fact]
    public async Task Unarchive_failure_after_the_record_is_gone_reports_Unarchived()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser("Target", UserState.Disabled);
        Guid key = target.Key;
        _f.Repository.InsertAuditAsync(Arg.Is<ArchiveAuditEntry>(e => e.UserKey == key)).ThrowsAsync(new InvalidOperationException("post-commit"));
        _f.Records[key] = new ArchivedUserRecord(key, DateTime.UtcNow, admin.Key, ArchiveSource.Manual, null, true);

        IReadOnlyList<UserArchiveResult> results = await _f.Create().UnarchiveAsync(admin.Key, new HashSet<Guid> { key }, null, ArchiveSource.Manual);

        Assert.Equal(ArchiveResultStatus.Unarchived, results[0].Status);
    }
}
