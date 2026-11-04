using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services.AuthorizationStatus;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Notifications;

namespace Umbraco.Community.ArchivedUsers.Tests;

public class ArchivedUserServiceUnarchiveTests
{
    private readonly ServiceFixture _f = new();

    private Task<IReadOnlyList<UserArchiveResult>> Unarchive(IUser performer, bool? enable, params IUser[] targets)
        => _f.Create().UnarchiveAsync(performer.Key, targets.Select(t => t.Key).ToHashSet(), enable, ArchiveSource.Manual);

    private IUser Archived(string name = "Jo", bool wasDisabledBefore = false, bool admin = false)
    {
        IUser user = admin ? _f.AddAdmin(name) : _f.AddUser(name);
        user.UserState.Returns(UserState.Disabled);
        user.IsApproved.Returns(false);
        _f.AddRecord(user, wasDisabledBefore);
        return user;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unarchive_by_disabled_or_missing_performer_returns_NotPermitted_for_every_key(bool performerExists)
    {
        IUser performer = _f.AddAdmin("Performer");
        performer.UserState.Returns(UserState.Disabled);
        if (!performerExists)
        {
            _f.RemoveUser(performer.Key);
        }

        IUser target = Archived();

        IReadOnlyList<UserArchiveResult> results = await Unarchive(performer, null, target);

        Assert.Equal(ArchiveResultStatus.NotPermitted, Assert.Single(results).Status);
        Assert.True(_f.Records.ContainsKey(target.Key));
        await _f.Repository.DidNotReceiveWithAnyArgs().DeleteAsync(default);
    }

    [Fact]
    public async Task Unarchive_holds_the_target_lock_around_delete_enable_and_audit()
    {
        _f.Settings.UnarchiveEnablesUser = true;
        IUser admin = _f.AddAdmin(), target = Archived();
        Guid key = target.Key;
        _f.Repository.When(r => r.DeleteAsync(key)).Do(_ => _f.Locks.Events.Add("delete"));
        _f.UserService.When(s => s.EnableAsync(Arg.Any<Guid>(), Arg.Any<ISet<Guid>>())).Do(_ => _f.Locks.Events.Add("enable"));
        _f.Repository.When(r => r.InsertAuditAsync(Arg.Any<ArchiveAuditEntry>())).Do(_ => _f.Locks.Events.Add("audit"));

        await Unarchive(admin, null, target);

        Assert.Equal(new[] { $"acquire:{key}", "delete", "enable", "audit", $"release:{key}" }, _f.Locks.Events);
    }

    /// <summary>D3: by default the user comes back disabled.</summary>
    [Fact]
    public async Task Unarchive_removes_record_writes_Unarchived_audit_and_leaves_user_disabled()
    {
        IUser admin = _f.AddAdmin(), target = Archived();

        IReadOnlyList<UserArchiveResult> results = await Unarchive(admin, null, target);

        Assert.Equal(ArchiveResultStatus.Unarchived, results[0].Status);
        Assert.False(_f.Records.ContainsKey(target.Key));
        await _f.UserService.DidNotReceiveWithAnyArgs().EnableAsync(default, default!);
        ArchiveAuditEntry row = Assert.Single(_f.AuditRows);
        Assert.Equal(ArchiveAuditAction.Unarchived, row.Action);
        Assert.False(row.ReEnabled);
        Assert.Equal(admin.Key, row.PerformedByUserKey);
    }

    /// <summary>
    /// Deleting first means the F7 handler, whose UserSaved notification is deferred to this scope's completion
    /// (docs/spikes.md S2), finds no record and writes no duplicate CoreEnable row (docs/design.md 7.2).
    /// </summary>
    [Fact]
    public async Task Unarchive_deletes_record_then_enables_then_writes_audit_in_one_scope()
    {
        _f.Settings.UnarchiveEnablesUser = true;
        IUser admin = _f.AddAdmin(), target = Archived();
        Guid adminKey = admin.Key, key = target.Key;

        await Unarchive(admin, null, target);

        Received.InOrder(() =>
        {
            _f.ScopeProvider.CreateCoreScope(
                Arg.Any<System.Data.IsolationLevel>(), Arg.Any<Umbraco.Cms.Core.Scoping.RepositoryCacheMode>(), Arg.Any<Umbraco.Cms.Core.Events.IEventDispatcher?>(),
                Arg.Any<Umbraco.Cms.Core.Events.IScopedNotificationPublisher?>(), Arg.Any<bool?>(), Arg.Any<bool>(), Arg.Any<bool>());
            _f.Repository.DeleteAsync(key);
            _f.UserService.EnableAsync(adminKey, Arg.Is<ISet<Guid>>(s => s.Single() == key));
            _f.Repository.InsertAuditAsync(Arg.Any<ArchiveAuditEntry>());
            _f.Scope.Complete();
        });
    }

    /// <summary>AU-07.</summary>
    [Fact]
    public async Task Unarchive_with_setting_on_and_WasDisabledBefore_false_enables()
    {
        _f.Settings.UnarchiveEnablesUser = true;
        IUser admin = _f.AddAdmin(), target = Archived(wasDisabledBefore: false);

        await Unarchive(admin, null, target);

        await _f.UserService.Received(1).EnableAsync(admin.Key, Arg.Any<ISet<Guid>>());
        Assert.True(Assert.Single(_f.AuditRows).ReEnabled);
    }

    [Fact]
    public async Task Unarchive_with_setting_on_and_WasDisabledBefore_true_stays_disabled()
    {
        _f.Settings.UnarchiveEnablesUser = true;
        IUser admin = _f.AddAdmin(), target = Archived(wasDisabledBefore: true);

        await Unarchive(admin, null, target);

        await _f.UserService.DidNotReceiveWithAnyArgs().EnableAsync(default, default!);
        Assert.False(_f.AuditRows[0].ReEnabled);
    }

    /// <summary>AU-11, T10: enable can only narrow the policy.</summary>
    [Fact]
    public async Task Unarchive_with_enable_true_and_WasDisabledBefore_true_stays_disabled()
    {
        _f.Settings.UnarchiveEnablesUser = true;
        IUser admin = _f.AddAdmin(), target = Archived(wasDisabledBefore: true);

        await Unarchive(admin, true, target);

        await _f.UserService.DidNotReceiveWithAnyArgs().EnableAsync(default, default!);
    }

    /// <summary>AU-11, T10: defence in depth; the controller already rejects this with 400.</summary>
    [Fact]
    public async Task Unarchive_with_enable_true_and_setting_off_does_not_enable()
    {
        IUser admin = _f.AddAdmin(), target = Archived();

        await Unarchive(admin, true, target);

        await _f.UserService.DidNotReceiveWithAnyArgs().EnableAsync(default, default!);
    }

    [Fact]
    public async Task Unarchive_with_enable_false_and_setting_on_stays_disabled()
    {
        _f.Settings.UnarchiveEnablesUser = true;
        IUser admin = _f.AddAdmin(), target = Archived();

        await Unarchive(admin, false, target);

        await _f.UserService.DidNotReceiveWithAnyArgs().EnableAsync(default, default!);
    }

    [Fact]
    public async Task Unarchive_not_archived_returns_NotArchived()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddUser(state: UserState.Disabled);

        Assert.Equal(ArchiveResultStatus.NotArchived, (await Unarchive(admin, null, target))[0].Status);
    }

    [Fact]
    public async Task Unarchive_invisible_returns_NotPermitted()
    {
        IUser manager = _f.AddUser("Manager"), admin = Archived("Archived admin", admin: true);

        Assert.Equal(ArchiveResultStatus.NotPermitted, (await Unarchive(manager, null, admin))[0].Status);
        Assert.True(_f.Records.ContainsKey(admin.Key));
    }

    /// <summary>AU-16.</summary>
    [Fact]
    public async Task Unarchive_unknown_returns_NotFound_to_admin_and_NotPermitted_to_non_admin()
    {
        IUser admin = _f.AddAdmin(), manager = _f.AddUser("Manager");
        var unknown = new HashSet<Guid> { Guid.NewGuid() };

        Assert.Equal(ArchiveResultStatus.NotFound, (await _f.Create().UnarchiveAsync(admin.Key, unknown, null, ArchiveSource.Manual))[0].Status);
        Assert.Equal(ArchiveResultStatus.NotPermitted, (await _f.Create().UnarchiveAsync(manager.Key, unknown, null, ArchiveSource.Manual))[0].Status);
    }

    /// <summary>AU-01.</summary>
    [Fact]
    public async Task Unarchive_refused_by_core_permission_rule_returns_NotPermitted_and_never_enables()
    {
        _f.Settings.UnarchiveEnablesUser = true;
        IUser manager = _f.AddUser("Manager"), target = Archived();
        _f.Permissions.AuthorizeAccessAsync(manager, Arg.Any<IEnumerable<Guid>>()).Returns(UserAuthorizationStatus.UnauthorizedMissingAdminAccess);

        IReadOnlyList<UserArchiveResult> results = await Unarchive(manager, null, target);

        Assert.Equal(ArchiveResultStatus.NotPermitted, results[0].Status);
        await _f.UserService.DidNotReceiveWithAnyArgs().EnableAsync(default, default!);
        Assert.True(_f.Records.ContainsKey(target.Key));
    }

    [Fact]
    public async Task Unarchive_writes_one_core_audit_entry()
    {
        IUser admin = _f.AddAdmin(), target = Archived();

        await Unarchive(admin, null, target);

        await _f.Audit.Received(1).AddAsync(AuditType.Custom, admin.Key, target.Id, "User", Arg.Is<string>(c => c.Contains("Unarchived")), Arg.Any<string?>());
    }

    [Fact]
    public async Task Enable_refused_by_core_still_unarchives_with_CoreStatus_and_ReEnabled_false()
    {
        _f.Settings.UnarchiveEnablesUser = true;
        IUser admin = _f.AddAdmin(), target = Archived();
        _f.UserService.EnableAsync(Arg.Any<Guid>(), Arg.Any<ISet<Guid>>()).Returns(UserOperationStatus.CannotDisableSelf);

        IReadOnlyList<UserArchiveResult> results = await Unarchive(admin, null, target);

        Assert.Equal(ArchiveResultStatus.Unarchived, results[0].Status);
        Assert.Equal(UserOperationStatus.CannotDisableSelf, results[0].CoreStatus);
        Assert.False(_f.AuditRows[0].ReEnabled);
        Assert.False(_f.Records.ContainsKey(target.Key));
    }

    [Fact]
    public async Task UserUnarchivedNotification_carries_ReEnabled()
    {
        _f.Settings.UnarchiveEnablesUser = true;
        IUser admin = _f.AddAdmin(), target = Archived();

        await Unarchive(admin, null, target);

        await _f.Events.Received(1).PublishAsync(
            Arg.Is<UserUnarchivedNotification>(n => n.Event.ReEnabled && n.Event.UserKey == target.Key && n.Event.Source == ArchiveSource.Manual),
            Arg.Any<CancellationToken>());
    }
}
