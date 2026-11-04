using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Security;
using Umbraco.Community.ArchivedUsers.Web.Filters;

namespace Umbraco.Community.ArchivedUsers.Tests;

/// <summary>Review BS-1: "disabled" means not approved, whatever UserState reports (LockedOut wins over Disabled in core).</summary>
public class LockedOutDisabledTests
{
    private readonly ServiceFixture _f = new();

    [Fact]
    public void IsDisabled_is_true_for_a_disabled_locked_out_user_and_false_for_invited_and_active()
    {
        Assert.True(UserStateRules.IsDisabled(_f.AddDisabledLockedOutUser()));
        Assert.True(UserStateRules.IsDisabled(_f.AddUser(state: UserState.Disabled)));
        Assert.False(UserStateRules.IsDisabled(_f.AddUser(state: UserState.Invited, loggedIn: false)));
        Assert.False(UserStateRules.IsDisabled(_f.AddUser()));
    }

    /// <summary>AU-11: otherwise UnarchiveEnablesUser would later re-enable a deliberately disabled user.</summary>
    [Fact]
    public async Task Archive_disabled_locked_out_user_sets_WasDisabledBefore_and_never_calls_core()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddDisabledLockedOutUser();

        IReadOnlyList<UserArchiveResult> results = await _f.Create().ArchiveAsync(admin.Key, new HashSet<Guid> { target.Key }, null, ArchiveSource.Manual);

        Assert.Equal(ArchiveResultStatus.Archived, results[0].Status);
        Assert.True(_f.Records[target.Key].WasDisabledBefore);
        await _f.UserService.DidNotReceiveWithAnyArgs().DisableAsync(default, default!);
    }

    [Fact]
    public async Task Archive_already_archived_disabled_locked_out_user_returns_AlreadyArchived_without_reconciling()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddDisabledLockedOutUser();
        _f.AddRecord(target);

        IReadOnlyList<UserArchiveResult> results = await _f.Create().ArchiveAsync(admin.Key, new HashSet<Guid> { target.Key }, null, ArchiveSource.Manual);

        Assert.Equal(ArchiveResultStatus.AlreadyArchived, results[0].Status);
        Assert.Empty(_f.AuditRows);
    }

    /// <summary>P9: a disabled performer is refused even when core reports them as LockedOut.</summary>
    [Fact]
    public async Task Disabled_locked_out_performer_gets_NotPermitted_everywhere()
    {
        IUser performer = _f.AddDisabledLockedOutUser("Performer", admin: true);
        IUser editor = _f.AddUser("Editor"), archived = _f.AddUser("Archived", UserState.Disabled);
        _f.AddRecord(archived);
        _f.AddUser("Former", UserState.Disabled);
        var service = _f.Create();

        Assert.Equal(ArchiveResultStatus.NotPermitted, (await service.ArchiveAsync(performer.Key, new HashSet<Guid> { editor.Key }, null, ArchiveSource.Manual))[0].Status);
        Assert.Equal(ArchiveResultStatus.NotPermitted, (await service.UnarchiveAsync(performer.Key, new HashSet<Guid> { archived.Key }, null, ArchiveSource.Manual))[0].Status);
        Assert.Equal(0, (await service.PreviewCleanUpAsync(performer.Key)).Count);
    }

    [Fact]
    public async Task Approved_but_locked_out_performer_gets_NotPermitted()
    {
        IUser performer = _f.AddAdmin("Performer");
        performer.IsLockedOut.Returns(true);
        performer.UserState.Returns(UserState.LockedOut);
        IUser editor = _f.AddUser("Editor");

        var results = await _f.Create().ArchiveAsync(performer.Key, new HashSet<Guid> { editor.Key }, null, ArchiveSource.Manual);

        Assert.Equal(ArchiveResultStatus.NotPermitted, results[0].Status);
    }

    [Fact]
    public async Task Archived_disabled_locked_out_user_is_not_flagged_stale()
    {
        IUser admin = _f.AddAdmin(), target = _f.AddDisabledLockedOutUser();
        _f.AddRecord(target);

        PagedModel<ArchivedUserView> page = await _f.Create().GetArchivedAsync(admin.Key, 0, 10, null, ArchivedUserOrder.ArchivedUtc, Direction.Descending);
        ArchivedUserView? single = await _f.Create().GetArchivedUserAsync(admin.Key, target.Key);

        Assert.False(page.Items.Single().IsStale);
        Assert.False(single!.IsStale);
    }

    [Fact]
    public void DropSet_drops_archived_disabled_locked_out_user()
    {
        IUser target = _f.AddDisabledLockedOutUser();

        Assert.Equal(new[] { target.Key }, UserListPaging.DropSet([target], new HashSet<Guid> { target.Key }));
    }

    [Fact]
    public async Task Clean_up_counts_disabled_locked_out_user()
    {
        IUser admin = _f.AddAdmin();
        IUser target = _f.AddDisabledLockedOutUser();
        // The legacy state query matches on the disabled flag, so these users come back from a Disabled query.
        _f.UserService.GetAll(Arg.Any<long>(), Arg.Any<int>(), out Arg.Any<long>(), Arg.Any<string>(), Arg.Any<Direction>(), Arg.Any<UserState[]?>(), Arg.Any<string[]?>(), Arg.Any<string?>())
            .Returns(ci => { ci[2] = 1L; return new[] { target }; });

        Assert.Equal(1, (await _f.Create().PreviewCleanUpAsync(admin.Key)).Count);
    }

    [Fact]
    public async Task Reconcile_keeps_record_of_disabled_locked_out_user()
    {
        IUser target = _f.AddDisabledLockedOutUser();
        _f.AddRecord(target);

        Assert.Equal(0, await _f.Create().ReconcileAsync(CancellationToken.None));
        Assert.True(_f.Records.ContainsKey(target.Key));
    }
}
