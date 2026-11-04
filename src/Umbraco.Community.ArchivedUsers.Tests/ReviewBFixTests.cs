using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Notifications;

namespace Umbraco.Community.ArchivedUsers.Tests;

/// <summary>Regression tests for milestone review B findings BS-2, BS-3, BQ-5 and BQ-7.</summary>
public class ReviewBFixTests
{
    private readonly ServiceFixture _f = new();

    /// <summary>BS-2: reconcile re-reads under the lock, so an archive that landed in between is kept.</summary>
    [Fact]
    public async Task Reconcile_keeps_a_record_whose_user_was_disabled_after_the_first_read()
    {
        IUser user = _f.AddUser("Racing");
        _f.AddRecord(user);
        _f.Locks.OnAcquire = key =>
        {
            if (key == user.Key)
            {
                user.UserState.Returns(UserState.Disabled);
                user.IsApproved.Returns(false);
            }
        };

        int removed = await _f.Create().ReconcileAsync(CancellationToken.None);

        Assert.Equal(0, removed);
        Assert.True(_f.Records.ContainsKey(user.Key));
        Assert.Empty(_f.AuditRows);
    }

    /// <summary>BS-3: a subscriber may archive any user without deadlocking on a held stripe.</summary>
    [Fact]
    public async Task Archived_notification_is_published_after_the_user_lock_is_released()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser();
        Guid key = target.Key;
        _f.Events.When(e => e.PublishAsync(Arg.Any<UserArchivedNotification>(), Arg.Any<CancellationToken>())).Do(_ => _f.Locks.Events.Add("publish"));

        await _f.Create().ArchiveAsync(admin.Key, new HashSet<Guid> { key }, null, ArchiveSource.Manual);

        Assert.True(_f.Locks.Events.IndexOf($"release:{key}") < _f.Locks.Events.IndexOf("publish"));
    }

    [Fact]
    public async Task Unarchived_notification_is_published_after_the_user_lock_is_released()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser(state: UserState.Disabled);
        _f.AddRecord(target);
        Guid key = target.Key;
        _f.Events.When(e => e.PublishAsync(Arg.Any<UserUnarchivedNotification>(), Arg.Any<CancellationToken>())).Do(_ => _f.Locks.Events.Add("publish"));

        await _f.Create().UnarchiveAsync(admin.Key, new HashSet<Guid> { key }, null, ArchiveSource.Manual);

        Assert.True(_f.Locks.Events.IndexOf($"release:{key}") < _f.Locks.Events.IndexOf("publish"));
    }

    /// <summary>BQ-5: one user's storage failure never turns a bulk call into a 500 that loses everyone's results.</summary>
    [Fact]
    public async Task Bulk_continues_after_a_storage_failure_for_an_already_disabled_user()
    {
        IUser admin = _f.AddAdmin();
        IUser first = _f.AddUser("First", UserState.Disabled), broken = _f.AddUser("Broken", UserState.Disabled), last = _f.AddUser("Last", UserState.Disabled);
        Guid brokenKey = broken.Key;
        _f.Repository.TryInsertAsync(Arg.Is<ArchivedUserRecord>(r => r.UserKey == brokenKey)).ThrowsAsync(new InvalidOperationException("db"));

        IReadOnlyList<UserArchiveResult> results = await _f.Create().ArchiveAsync(admin.Key, new HashSet<Guid> { first.Key, brokenKey, last.Key }, null, ArchiveSource.Bulk);

        // Review C-7: an already-disabled user is unchanged by the failure, so Failed (not CoreRefused).
        Assert.Equal(new[] { ArchiveResultStatus.Archived, ArchiveResultStatus.Failed, ArchiveResultStatus.Archived }, results.Select(r => r.Status));
        Assert.Null(results[1].CoreStatus);
    }

    /// <summary>BQ-7: key lookups are chunked so SQL Server's parameter limit (about 2,100) is never hit.</summary>
    [Fact]
    public async Task Archived_view_loads_users_in_chunks_of_at_most_500()
    {
        IUser admin = _f.AddAdmin();
        for (var i = 0; i < 1200; i++)
        {
            _f.Records[Guid.NewGuid()] = new ArchivedUserRecord(Guid.NewGuid(), DateTime.UtcNow, null, ArchiveSource.Manual, null, false);
        }

        await _f.Create().GetArchivedAsync(admin.Key, 0, 10, null, ArchivedUserOrder.ArchivedUtc, Direction.Descending);
        await _f.Create().ReconcileAsync(CancellationToken.None);

        var batchSizes = _f.UserService.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "GetAsync" && c.GetArguments()[0] is IEnumerable<Guid>)
            .Select(c => ((IEnumerable<Guid>)c.GetArguments()[0]!).Count())
            .ToList();
        Assert.NotEmpty(batchSizes);
        Assert.All(batchSizes, size => Assert.True(size <= 500, $"batch of {size}"));
    }
}
