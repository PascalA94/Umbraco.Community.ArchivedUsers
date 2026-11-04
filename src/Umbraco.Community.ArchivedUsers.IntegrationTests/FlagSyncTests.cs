using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.ArchivedUsers.Core.Configuration;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Notifications;
using Umbraco.Community.ArchivedUsers.Core.Persistence;
using Umbraco.Community.ArchivedUsers.Core.Services;

namespace Umbraco.Community.ArchivedUsers.IntegrationTests;

/// <summary>Shared helpers for tests that read and write the package tables directly.</summary>
public abstract class ArchiveTableTestBase : ArchivedUsersIntegrationTestBase
{
    protected IUserService UserService => GetRequiredService<IUserService>();

    protected IArchivedUserService Service => GetRequiredService<IArchivedUserService>();

    [SetUp]
    public async Task Migrate() => await RunPackageMigrationsAsync();

    private protected async Task<T> InScope<T>(Func<IArchivedUserRepository, Task<T>> work)
    {
        using ICoreScope scope = GetRequiredService<ICoreScopeProvider>().CreateCoreScope();
        T result = await work(GetRequiredService<IArchivedUserRepository>());
        scope.Complete();
        return result;
    }

    protected Task<ArchivedUserRecord?> RecordAsync(Guid key) => InScope(r => r.GetAsync(key));

    protected Task<IReadOnlyList<ArchiveAuditEntry>> AuditAsync(Guid key) => InScope(r => r.GetAuditForUserAsync(key));

    protected Task InsertRecordAsync(Guid key, bool wasDisabledBefore = false)
        => InScope(r => r.TryInsertAsync(new ArchivedUserRecord(key, DateTime.UtcNow, null, ArchiveSource.Manual, null, wasDisabledBefore)));

    protected async Task<IUser> ArchivedEditorAsync(IUser admin, bool hasLoggedIn = true)
    {
        IUser editor = await Users.CreateEditorAsync(hasLoggedIn: hasLoggedIn);
        var result = await Service.ArchiveAsync(admin.Key, new HashSet<Guid> { editor.Key }, "Left", ArchiveSource.Manual);
        Assert.That(result.Single().Status, Is.EqualTo(ArchiveResultStatus.Archived));
        return editor;
    }
}

[TestFixture]
public class FlagSyncTests : ArchiveTableTestBase
{
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.AddSingleton<UnarchivedRecorder>();
        builder.AddNotificationHandler<UserUnarchivedNotification, UnarchivedRecorder.Handler>();
    }

    /// <summary>T11, AU-07: the admin who pressed core's Enable is the recorded performer.</summary>
    [Test]
    public async Task Core_enable_of_archived_user_by_logged_in_admin_writes_CoreEnable_with_that_admin_as_performer()
    {
        IUser admin = await Users.CreateAdminAsync(), other = await Users.CreateAdminAsync("Other admin");
        IUser editor = await ArchivedEditorAsync(admin);
        var security = Mock.Of<IBackOfficeSecurity>(s => s.CurrentUser == other);

        // The performer argument differs from the ambient user, so the test proves the handler reads the
        // signed-in backoffice user rather than anything core passed (review BS-6).
        using (GetRequiredService<IBackOfficeSecurityAccessor>().Override(security))
        {
            await UserService.EnableAsync(Constants.Security.SuperUserKey, new HashSet<Guid> { editor.Key });
        }

        Assert.That(await RecordAsync(editor.Key), Is.Null);
        ArchiveAuditEntry row = (await AuditAsync(editor.Key)).First();
        Assert.That(row.Action, Is.EqualTo(ArchiveAuditAction.Unarchived));
        Assert.That(row.Source, Is.EqualTo(ArchiveSource.CoreEnable));
        Assert.That(row.PerformedByUserKey, Is.EqualTo(other.Key));
        Assert.That(row.ReEnabled, Is.True);
    }

    [Test]
    public async Task Core_enable_without_backoffice_user_writes_CoreEnable_with_null_performer()
    {
        IUser admin = await Users.CreateAdminAsync();
        IUser editor = await ArchivedEditorAsync(admin);

        await UserService.EnableAsync(Constants.Security.SuperUserKey, new HashSet<Guid> { editor.Key });

        ArchiveAuditEntry row = (await AuditAsync(editor.Key)).First();
        Assert.That(row.Source, Is.EqualTo(ArchiveSource.CoreEnable));
        Assert.That(row.PerformedByUserKey, Is.Null);
    }

    /// <summary>Review BS-1: a disabled user who is also locked out is still disabled; a save keeps the flag.</summary>
    [Test]
    public async Task Saving_an_archived_disabled_locked_out_user_keeps_the_flag()
    {
        IUser admin = await Users.CreateAdminAsync();
        IUser editor = await ArchivedEditorAsync(admin);

        IUser locked = await Users.DisableAndLockOutAsync(editor);

        Assert.That(locked.UserState, Is.EqualTo(UserState.LockedOut), "core reports LockedOut before Disabled");
        Assert.That(await RecordAsync(editor.Key), Is.Not.Null);
        Assert.That(await AuditAsync(editor.Key), Has.Count.EqualTo(1));
        Assert.That(await Service.ReconcileAsync(CancellationToken.None), Is.EqualTo(0));
        Assert.That(await RecordAsync(editor.Key), Is.Not.Null);
    }

    [Test]
    public async Task Saving_an_archived_user_that_stays_disabled_changes_nothing()
    {
        IUser admin = await Users.CreateAdminAsync();
        IUser editor = await ArchivedEditorAsync(admin);
        IUser reloaded = (await UserService.GetAsync(editor.Key))!;
        reloaded.Name = "Renamed";

        UserService.Save(reloaded);

        Assert.That(await RecordAsync(editor.Key), Is.Not.Null);
        Assert.That(await AuditAsync(editor.Key), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Deleting_a_never_logged_in_archived_user_removes_record_and_keeps_audit()
    {
        IUser admin = await Users.CreateAdminAsync();
        IUser editor = await ArchivedEditorAsync(admin, hasLoggedIn: false);

        var status = await UserService.DeleteAsync(admin.Key, editor.Key);

        Assert.That(status, Is.EqualTo(Umbraco.Cms.Core.Services.OperationStatus.UserOperationStatus.Success));
        Assert.That(await RecordAsync(editor.Key), Is.Null);
        Assert.That(await AuditAsync(editor.Key), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Core_enable_publishes_UserUnarchivedNotification_with_source_CoreEnable()
    {
        IUser admin = await Users.CreateAdminAsync();
        IUser editor = await ArchivedEditorAsync(admin);
        var recorder = GetRequiredService<UnarchivedRecorder>();

        await UserService.EnableAsync(admin.Key, new HashSet<Guid> { editor.Key });

        Assert.That(recorder.Events.Count(e => e.UserKey == editor.Key && e.Source == ArchiveSource.CoreEnable && e.ReEnabled), Is.EqualTo(1));
    }
}

/// <summary>F7 with the site setting on: the package's own unarchive writes one row, not a second CoreEnable row.</summary>
[TestFixture]
public class FlagSyncWithUnarchiveEnablesTests : ArchiveTableTestBase
{
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.PostConfigure<ArchivedUsersSettings>(s => s.UnarchiveEnablesUser = true);
    }

    [Test]
    public async Task Package_unarchive_with_enable_writes_exactly_one_Unarchived_row()
    {
        IUser admin = await Users.CreateAdminAsync();
        IUser editor = await ArchivedEditorAsync(admin);

        var result = await Service.UnarchiveAsync(admin.Key, new HashSet<Guid> { editor.Key }, null, ArchiveSource.Manual);

        Assert.That(result.Single().Status, Is.EqualTo(ArchiveResultStatus.Unarchived));
        Assert.That((await UserService.GetAsync(editor.Key))!.IsApproved, Is.True);
        IReadOnlyList<ArchiveAuditEntry> rows = await AuditAsync(editor.Key);
        Assert.That(rows.Count(r => r.Action == ArchiveAuditAction.Unarchived), Is.EqualTo(1));
        Assert.That(rows.Any(r => r.Source == ArchiveSource.CoreEnable), Is.False);
        Assert.That(rows.First().ReEnabled, Is.True);
    }
}

public sealed class UnarchivedRecorder
{
    public ConcurrentBag<UserUnarchiveEvent> Events { get; } = new();

    public sealed class Handler(UnarchivedRecorder r) : INotificationHandler<UserUnarchivedNotification>
    {
        public void Handle(UserUnarchivedNotification n) => r.Events.Add(n.Event);
    }
}
