using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services.AuthorizationStatus;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Notifications;
using Umbraco.Community.ArchivedUsers.Core.Services;

namespace Umbraco.Community.ArchivedUsers.Tests;

public class ArchivedUserServiceArchiveTests
{
    private readonly ServiceFixture _f = new();

    private Task<IReadOnlyList<UserArchiveResult>> Archive(IUser performer, params IUser[] targets)
        => _f.Create().ArchiveAsync(performer.Key, targets.Select(t => t.Key).ToHashSet(), "Left 24 Oct, SUP-123", ArchiveSource.Manual);

    private static ISet<Guid> Only(Guid key) => Arg.Is<ISet<Guid>>(s => s.Count == 1 && s.Contains(key));

    [Fact]
    public async Task Archive_enabled_user_disables_then_writes_record_and_one_audit_row()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser("Jo Bloggs");

        IReadOnlyList<UserArchiveResult> results = await Archive(admin, target);

        await _f.UserService.Received(1).DisableAsync(admin.Key, Only(target.Key));
        ArchivedUserRecord record = _f.Records[target.Key];
        Assert.False(record.WasDisabledBefore);
        Assert.Equal(ArchiveSource.Manual, record.Source);
        Assert.Equal(admin.Key, record.ArchivedByUserKey);
        Assert.Equal(_f.Time.GetUtcNow().UtcDateTime, record.ArchivedUtc);
        Assert.Equal("Left 24 Oct, SUP-123", record.Reason);
        ArchiveAuditEntry row = Assert.Single(_f.AuditRows);
        Assert.Equal(ArchiveAuditAction.Archived, row.Action);
        Assert.Equal("Jo Bloggs", row.UserNameSnapshot);
        Assert.Equal(admin.Key, row.PerformedByUserKey);
        Assert.Equal(ArchiveResultStatus.Archived, Assert.Single(results).Status);
    }

    [Fact]
    public async Task Archive_disabled_user_does_not_call_core_and_sets_WasDisabledBefore()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser(state: UserState.Disabled);

        IReadOnlyList<UserArchiveResult> results = await Archive(admin, target);

        await _f.UserService.DidNotReceiveWithAnyArgs().DisableAsync(default, default!);
        Assert.True(_f.Records[target.Key].WasDisabledBefore);
        Assert.Equal(ArchiveResultStatus.Archived, results[0].Status);
    }

    [Fact]
    public async Task Archive_self_returns_Self_and_writes_nothing()
    {
        IUser admin = _f.AddAdmin();

        IReadOnlyList<UserArchiveResult> results = await Archive(admin, admin);

        Assert.Equal(ArchiveResultStatus.Self, results[0].Status);
        Assert.Empty(_f.Records);
        Assert.Empty(_f.AuditRows);
    }

    [Fact]
    public async Task Archive_invited_user_returns_Invited_and_writes_nothing()
    {
        IUser admin = _f.AddAdmin(), invited = _f.AddUser(state: UserState.Invited, loggedIn: false);

        IReadOnlyList<UserArchiveResult> results = await Archive(admin, invited);

        Assert.Equal(ArchiveResultStatus.Invited, results[0].Status);
        Assert.Empty(_f.Records);
        await _f.UserService.DidNotReceiveWithAnyArgs().DisableAsync(default, default!);
    }

    [Fact]
    public async Task Archive_already_archived_returns_AlreadyArchived()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser(state: UserState.Disabled);
        _f.AddRecord(target);

        IReadOnlyList<UserArchiveResult> results = await Archive(admin, target);

        Assert.Equal(ArchiveResultStatus.AlreadyArchived, results[0].Status);
        Assert.Empty(_f.AuditRows);
    }

    /// <summary>AU-03: a record for an enabled user is reconciled, then the archive proceeds as new.</summary>
    [Fact]
    public async Task Archive_stale_record_reconciles_then_archives()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser();
        _f.AddRecord(target, wasDisabledBefore: true);

        IReadOnlyList<UserArchiveResult> results = await Archive(admin, target);

        Assert.Equal(ArchiveResultStatus.Archived, results[0].Status);
        Assert.Equal(2, _f.AuditRows.Count);
        Assert.Equal(ArchiveAuditAction.Unarchived, _f.AuditRows[0].Action);
        Assert.Equal(ArchiveSource.Reconcile, _f.AuditRows[0].Source);
        Assert.Null(_f.AuditRows[0].PerformedByUserKey);
        Assert.Equal(ArchiveAuditAction.Archived, _f.AuditRows[1].Action);
        await _f.UserService.Received(1).DisableAsync(admin.Key, Only(target.Key));
        Assert.False(_f.Records[target.Key].WasDisabledBefore);
    }

    [Fact]
    public async Task Archive_unknown_key_returns_NotFound_to_admin()
    {
        IUser admin = _f.AddAdmin();

        IReadOnlyList<UserArchiveResult> results = await _f.Create().ArchiveAsync(admin.Key, new HashSet<Guid> { Guid.NewGuid() }, null, ArchiveSource.Manual);

        Assert.Equal(ArchiveResultStatus.NotFound, results[0].Status);
    }

    /// <summary>AU-16, T13: a non-admin can't tell an unknown key from an admin's key.</summary>
    [Fact]
    public async Task Archive_unknown_key_returns_NotPermitted_to_non_admin()
    {
        IUser manager = _f.AddUser("Manager");

        IReadOnlyList<UserArchiveResult> results = await _f.Create().ArchiveAsync(manager.Key, new HashSet<Guid> { Guid.NewGuid() }, null, ArchiveSource.Manual);

        Assert.Equal(ArchiveResultStatus.NotPermitted, results[0].Status);
    }

    [Fact]
    public async Task Archive_invisible_target_returns_NotPermitted_and_never_calls_core()
    {
        IUser manager = _f.AddUser("Manager"), admin = _f.AddAdmin();

        IReadOnlyList<UserArchiveResult> results = await Archive(manager, admin);

        Assert.Equal(ArchiveResultStatus.NotPermitted, results[0].Status);
        await _f.UserService.DidNotReceiveWithAnyArgs().DisableAsync(default, default!);
        Assert.Empty(_f.Records);
    }

    /// <summary>AU-01: core's permission rule is checked as well as visibility.</summary>
    [Fact]
    public async Task Archive_refused_by_core_permission_rule_returns_NotPermitted_and_never_calls_DisableAsync()
    {
        IUser manager = _f.AddUser("Manager"), target = _f.AddUser("Editor");
        _f.Permissions.AuthorizeAccessAsync(manager, Arg.Any<IEnumerable<Guid>>()).Returns(UserAuthorizationStatus.UnauthorizedMissingAdminAccess);

        IReadOnlyList<UserArchiveResult> results = await Archive(manager, target);

        Assert.Equal(ArchiveResultStatus.NotPermitted, results[0].Status);
        await _f.UserService.DidNotReceiveWithAnyArgs().DisableAsync(default, default!);
        Assert.Empty(_f.Records);
        Assert.Empty(_f.AuditRows);
    }

    /// <summary>AU-01, T1: the superuser is never a target.</summary>
    [Fact]
    public async Task Archive_superuser_target_by_admin_returns_NotPermitted()
    {
        IUser admin = _f.AddAdmin(), super = _f.AddSuperUser();

        IReadOnlyList<UserArchiveResult> results = await Archive(admin, super);

        Assert.Equal(ArchiveResultStatus.NotPermitted, results[0].Status);
        await _f.UserService.DidNotReceiveWithAnyArgs().DisableAsync(default, default!);
    }

    /// <summary>P9: a disabled (for example just-archived) or missing performer is refused for every key.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Archive_by_disabled_or_missing_performer_returns_NotPermitted_for_every_key(bool performerExists)
    {
        IUser performer = _f.AddAdmin("Performer");
        performer.UserState.Returns(UserState.Disabled);
        if (!performerExists)
        {
            _f.RemoveUser(performer.Key);
        }

        IUser editor = _f.AddUser("Editor"), admin = _f.AddAdmin("Other admin");

        IReadOnlyList<UserArchiveResult> results = await Archive(performer, editor, admin);

        Assert.All(results, r => Assert.Equal(ArchiveResultStatus.NotPermitted, r.Status));
        Assert.Equal(2, results.Count);
        await _f.UserService.DidNotReceiveWithAnyArgs().DisableAsync(default, default!);
        await _f.Repository.DidNotReceiveWithAnyArgs().TryInsertAsync(default!);
        await _f.Permissions.DidNotReceiveWithAnyArgs().AuthorizeAccessAsync(default!, default(IEnumerable<Guid>)!);
    }

    /// <summary>AU-02, T4c.</summary>
    [Fact]
    public async Task Archive_rotates_security_stamp_after_disable()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser();

        await Archive(admin, target);

        // Substitute properties read inside Received.InOrder become part of the query, so capture keys first.
        Guid adminKey = admin.Key, targetKey = target.Key;
        Received.InOrder(() =>
        {
            _f.UserService.DisableAsync(adminKey, Only(targetKey));
            _f.StampRotator.RotateAsync(targetKey);
        });
    }

    [Fact]
    public async Task Archive_already_disabled_user_does_not_rotate_stamp()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser(state: UserState.Disabled);

        await Archive(admin, target);

        await _f.StampRotator.DidNotReceiveWithAnyArgs().RotateAsync(default);
    }

    /// <summary>AU-02: a stamp failure is not fatal; core already revoked tokens and refuses disabled users.</summary>
    [Fact]
    public async Task Stamp_rotation_failure_logs_warning_and_still_archives()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser();
        _f.StampRotator.RotateAsync(target.Key).ThrowsAsync(new InvalidOperationException("boom"));

        IReadOnlyList<UserArchiveResult> results = await Archive(admin, target);

        Assert.Equal(ArchiveResultStatus.Archived, results[0].Status);
        Assert.True(_f.Records.ContainsKey(target.Key));
        Assert.Single(_f.AuditRows);
        Assert.Equal(1, _f.LogCount(LogLevel.Warning));
    }

    /// <summary>AU-07: a second record outside the package's tables, without reason or name.</summary>
    [Fact]
    public async Task Archive_writes_one_core_audit_entry()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser("Secret Name");

        await Archive(admin, target);

        await _f.Audit.Received(1).AddAsync(
            AuditType.Custom,
            admin.Key,
            target.Id,
            "User",
            Arg.Is<string>(c => c.Contains("Archived") && c.Contains("Manual") && !c.Contains("SUP-123") && !c.Contains("Secret Name")),
            Arg.Any<string?>());
    }

    /// <summary>AU-09, AU-12: logs carry user keys only.</summary>
    [Fact]
    public async Task Logs_never_contain_reason_or_name()
    {
        IUser admin = _f.AddAdmin();
        IUser cancelled = _f.AddUser("Secret Name"), refused = _f.AddUser("Secret Name"), broken = _f.AddUser("Secret Name");
        _f.Events.PublishCancelableAsync(Arg.Is<UserArchivingNotification>(n => n.Event.UserKey == cancelled.Key)).Returns(true);
        _f.UserService.DisableAsync(admin.Key, Only(refused.Key)).Returns(UserOperationStatus.CannotDisableInvitedUser);
        _f.Repository.TryInsertAsync(Arg.Is<ArchivedUserRecord>(r => r.UserKey == broken.Key)).ThrowsAsync(new InvalidOperationException("db down"));

        await _f.Create().ArchiveAsync(admin.Key, new HashSet<Guid> { cancelled.Key, refused.Key, broken.Key }, "secret-reason", ArchiveSource.Bulk);

        var text = string.Join("\n", _f.LogText());
        Assert.DoesNotContain("secret-reason", text);
        Assert.DoesNotContain("Secret Name", text);
        Assert.Contains(broken.Key.ToString(), text);
    }

    [Fact]
    public async Task Core_refusal_returns_CoreRefused_with_status_and_writes_nothing()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser();
        _f.UserService.DisableAsync(admin.Key, Only(target.Key)).Returns(UserOperationStatus.CannotDisableInvitedUser);

        IReadOnlyList<UserArchiveResult> results = await Archive(admin, target);

        Assert.Equal(ArchiveResultStatus.CoreRefused, results[0].Status);
        Assert.Equal(UserOperationStatus.CannotDisableInvitedUser, results[0].CoreStatus);
        Assert.Empty(_f.Records);
        Assert.Empty(_f.AuditRows);
    }

    [Fact]
    public async Task Cancelled_notification_returns_Cancelled_with_message_and_never_calls_core()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser();
        _f.Events.PublishCancelableAsync(Arg.Any<UserArchivingNotification>()).Returns(ci =>
        {
            ci.Arg<UserArchivingNotification>().CancelOperation(new EventMessage("Policy", "Locked by HR", EventMessageType.Error));
            return true;
        });

        IReadOnlyList<UserArchiveResult> results = await Archive(admin, target);

        Assert.Equal(ArchiveResultStatus.Cancelled, results[0].Status);
        Assert.Equal("Locked by HR", results[0].Message);
        await _f.UserService.DidNotReceiveWithAnyArgs().DisableAsync(default, default!);
        Assert.Empty(_f.Records);
    }

    /// <summary>Review Focus 1: another node won the unique index; no second record, no 500.</summary>
    [Fact]
    public async Task Insert_collision_after_disable_returns_AlreadyArchived_and_logs_warning()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser();
        _f.Repository.TryInsertAsync(Arg.Any<ArchivedUserRecord>()).Returns(false);

        IReadOnlyList<UserArchiveResult> results = await Archive(admin, target);

        Assert.Equal(ArchiveResultStatus.AlreadyArchived, results[0].Status);
        Assert.Empty(_f.AuditRows);
        Assert.Equal(1, _f.LogCount(LogLevel.Warning));
    }

    /// <summary>P10: the accepted gap (design 7.2). The user is disabled but not archived; that fails safe.</summary>
    [Fact]
    public async Task Scope_failure_after_disable_logs_one_Error_and_returns_CoreRefused_without_CoreStatus()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser();
        _f.Repository.TryInsertAsync(Arg.Any<ArchivedUserRecord>()).ThrowsAsync(new InvalidOperationException("db down"));

        IReadOnlyList<UserArchiveResult> results = await Archive(admin, target);

        Assert.Equal(ArchiveResultStatus.CoreRefused, results[0].Status);
        Assert.Null(results[0].CoreStatus);
        Assert.Equal(1, _f.LogCount(LogLevel.Error));
        Assert.Contains(target.Key.ToString(), string.Join(" ", _f.LogText()));
        await _f.Events.DidNotReceive().PublishAsync(Arg.Any<UserArchivedNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// P10: the accepted gap's consequence. The retry archives the now-disabled user with WasDisabledBefore = true,
    /// so a later unarchive keeps them disabled, which fails safe (design 7.2).
    /// </summary>
    [Fact]
    public async Task Scope_failure_then_retry_archives_the_now_disabled_user()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser();
        _f.Repository.TryInsertAsync(Arg.Any<ArchivedUserRecord>()).ThrowsAsync(new InvalidOperationException("db down"));
        await Archive(admin, target);
        _f.UserService.ClearReceivedCalls();
        _f.Repository.TryInsertAsync(Arg.Any<ArchivedUserRecord>()).Returns(ci => _f.Records.TryAdd(ci.Arg<ArchivedUserRecord>().UserKey, ci.Arg<ArchivedUserRecord>()));

        IReadOnlyList<UserArchiveResult> results = await Archive(admin, target);

        Assert.Equal(ArchiveResultStatus.Archived, results[0].Status);
        await _f.UserService.DidNotReceiveWithAnyArgs().DisableAsync(default, default!);
        Assert.True(_f.Records[target.Key].WasDisabledBefore);
    }

    /// <summary>P6: the target's lock is held from the state read to the record write.</summary>
    [Fact]
    public async Task Archive_holds_the_target_lock_from_state_read_to_record_write()
    {
        IUser admin = _f.AddAdmin(), a = _f.AddUser("A"), b = _f.AddUser("B");
        _f.UserService.When(s => s.GetAsync(a.Key)).Do(_ => _f.Locks.Events.Add("read-user:a"));
        _f.Repository.When(r => r.GetAsync(a.Key)).Do(_ => _f.Locks.Events.Add("read-record:a"));
        _f.Repository.When(r => r.InsertAuditAsync(Arg.Is<ArchiveAuditEntry>(e => e.UserKey == a.Key))).Do(_ => _f.Locks.Events.Add("audit:a"));

        await Archive(admin, a, b);

        List<string> e = _f.Locks.Events;
        Assert.True(e.IndexOf($"acquire:{a.Key}") < e.IndexOf("read-user:a"));
        Assert.True(e.IndexOf("read-user:a") < e.IndexOf("read-record:a"));
        Assert.True(e.IndexOf("audit:a") < e.IndexOf($"release:{a.Key}"));
        Assert.Equal(1, e.Count(x => x == $"acquire:{a.Key}"));
        Assert.Equal(1, e.Count(x => x == $"acquire:{b.Key}"));
        Assert.Equal(1, _f.Locks.MaxHeldAtOnce);
    }

    [Fact]
    public async Task Bulk_returns_one_result_per_key_and_continues_after_a_skip()
    {
        IUser admin = _f.AddAdmin(), invited = _f.AddUser(state: UserState.Invited, loggedIn: false), ok1 = _f.AddUser("ok1"), ok2 = _f.AddUser("ok2");

        IReadOnlyList<UserArchiveResult> results = await Archive(admin, admin, invited, ok1, ok2);

        Assert.Equal(
            new[] { ArchiveResultStatus.Self, ArchiveResultStatus.Invited, ArchiveResultStatus.Archived, ArchiveResultStatus.Archived },
            results.Select(r => r.Status));
        Assert.Equal(new[] { admin.Key, invited.Key, ok1.Key, ok2.Key }, results.Select(r => r.Id));
    }

    [Fact]
    public async Task Archived_notification_published_after_scope_complete()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser();

        await Archive(admin, target);

        Guid adminKey = admin.Key, targetKey = target.Key;
        Received.InOrder(() =>
        {
            _f.Scope.Complete();
            _f.Events.PublishAsync(Arg.Is<UserArchivedNotification>(n => n.Event.UserKey == targetKey && n.Event.PerformerKey == adminKey), Arg.Any<CancellationToken>());
        });
    }
}
