using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Notifications;
using Umbraco.Community.ArchivedUsers.Core.Persistence;
using Umbraco.Community.ArchivedUsers.Core.Services;

namespace Umbraco.Community.ArchivedUsers.IntegrationTests;

[TestFixture]
public class ArchiveFlowTests : ArchivedUsersIntegrationTestBase
{
    private IArchivedUserService Service => GetRequiredService<IArchivedUserService>();

    private IUserService UserService => GetRequiredService<IUserService>();

    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.AddSingleton<ArchiveNotificationRecorder>();
        builder.AddNotificationHandler<UserArchivingNotification, ArchiveNotificationRecorder.Archiving>();
        builder.AddNotificationHandler<UserArchivedNotification, ArchiveNotificationRecorder.Archived>();
    }

    [SetUp]
    public async Task Migrate() => await RunPackageMigrationsAsync();

    private async Task<ArchivedUserRecord?> RecordAsync(Guid key)
    {
        using ICoreScope scope = GetRequiredService<ICoreScopeProvider>().CreateCoreScope(autoComplete: true);
        return await GetRequiredService<IArchivedUserRepository>().GetAsync(key);
    }

    private async Task<IReadOnlyList<ArchiveAuditEntry>> AuditAsync(Guid key)
    {
        using ICoreScope scope = GetRequiredService<ICoreScopeProvider>().CreateCoreScope(autoComplete: true);
        return await GetRequiredService<IArchivedUserRepository>().GetAuditForUserAsync(key);
    }

    private async Task<UserArchiveResult> ArchiveOne(IUser performer, IUser target)
        => (await Service.ArchiveAsync(performer.Key, new HashSet<Guid> { target.Key }, "Left", ArchiveSource.Manual)).Single();

    [Test]
    public async Task Archive_disables_user_and_persists_record_and_audit()
    {
        IUser admin = await Users.CreateAdminAsync(), editor = await Users.CreateEditorAsync();

        UserArchiveResult result = await ArchiveOne(admin, editor);

        Assert.That(result.Status, Is.EqualTo(ArchiveResultStatus.Archived));
        Assert.That((await UserService.GetAsync(editor.Key))!.UserState, Is.EqualTo(UserState.Disabled));
        Assert.That(await RecordAsync(editor.Key), Is.Not.Null);
        Assert.That(await AuditAsync(editor.Key), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Archiving_and_archived_notifications_fire_once_each()
    {
        IUser admin = await Users.CreateAdminAsync(), editor = await Users.CreateEditorAsync();
        var recorder = GetRequiredService<ArchiveNotificationRecorder>();

        await ArchiveOne(admin, editor);

        Assert.That(recorder.ArchivingKeys.Count(k => k == editor.Key), Is.EqualTo(1));
        Assert.That(recorder.ArchivedKeys.Count(k => k == editor.Key), Is.EqualTo(1));
    }

    [Test]
    public async Task Archive_invited_user_returns_Invited_and_writes_nothing()
    {
        IUser admin = await Users.CreateAdminAsync(), invited = await Users.CreateEditorAsync(state: UserState.Invited, hasLoggedIn: false);

        UserArchiveResult result = await ArchiveOne(admin, invited);

        Assert.That(result.Status, Is.EqualTo(ArchiveResultStatus.Invited));
        Assert.That(await RecordAsync(invited.Key), Is.Null);
        Assert.That(await AuditAsync(invited.Key), Is.Empty);
    }

    /// <summary>T1, AU-01: core's DisableAsync would succeed here (tripwire); the package refuses.</summary>
    [Test]
    public async Task Non_admin_cannot_archive_admin_and_admin_stays_enabled()
    {
        IUser manager = await Users.CreateUserManagerAsync(), admin = await Users.CreateAdminAsync();

        UserArchiveResult result = await ArchiveOne(manager, admin);

        Assert.That(result.Status, Is.EqualTo(ArchiveResultStatus.NotPermitted));
        Assert.That((await UserService.GetAsync(admin.Key))!.IsApproved, Is.True);
    }

    [Test]
    public async Task Admin_cannot_archive_superuser()
    {
        IUser admin = await Users.CreateAdminAsync();

        UserArchiveResult result = (await Service.ArchiveAsync(admin.Key, new HashSet<Guid> { Constants.Security.SuperUserKey }, null, ArchiveSource.Manual)).Single();

        Assert.That(result.Status, Is.EqualTo(ArchiveResultStatus.NotPermitted));
        Assert.That((await UserService.GetAsync(Constants.Security.SuperUserKey))!.IsApproved, Is.True);
    }

    /// <summary>T4c, AU-02.</summary>
    [Test]
    public async Task Archive_changes_security_stamp()
    {
        IUser admin = await Users.CreateAdminAsync(), editor = await Users.CreateEditorAsync();
        string? before = (await UserService.GetAsync(editor.Key))!.SecurityStamp;

        await ArchiveOne(admin, editor);

        Assert.That((await UserService.GetAsync(editor.Key))!.SecurityStamp, Is.Not.EqualTo(before));
    }

    /// <summary>P9: once A has archived B, B (now disabled) can't archive A.</summary>
    [Test]
    public async Task Archived_admin_cannot_then_archive_the_other_admin()
    {
        IUser a = await Users.CreateAdminAsync("Admin A"), b = await Users.CreateAdminAsync("Admin B");

        Assert.That((await ArchiveOne(a, b)).Status, Is.EqualTo(ArchiveResultStatus.Archived));
        Assert.That((await ArchiveOne(b, a)).Status, Is.EqualTo(ArchiveResultStatus.NotPermitted));

        Assert.That((await UserService.GetAsync(a.Key))!.IsApproved, Is.True);
        Assert.That(await RecordAsync(a.Key), Is.Null);
    }

    /// <summary>P6, T16, Review Focus 1. Runs on SQLite in every build and on SQL Server nightly; never skipped.</summary>
    [Test]
    [Category("Concurrency")]
    public async Task Concurrent_double_archive_leaves_one_record_one_audit_row_no_exception()
    {
        IUser admin = await Users.CreateAdminAsync(), editor = await Users.CreateEditorAsync();

        async Task<UserArchiveResult> ArchiveInOwnScope()
        {
            using IServiceScope scope = Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IArchivedUserService>();
            return (await service.ArchiveAsync(admin.Key, new HashSet<Guid> { editor.Key }, "Left", ArchiveSource.Manual)).Single();
        }

        UserArchiveResult[] results = await Task.WhenAll(Task.Run(ArchiveInOwnScope), Task.Run(ArchiveInOwnScope));

        Assert.That(results.Select(r => r.Status), Is.EquivalentTo(new[] { ArchiveResultStatus.Archived, ArchiveResultStatus.AlreadyArchived }));
        ArchivedUserRecord? record = await RecordAsync(editor.Key);
        Assert.That(record, Is.Not.Null);
        Assert.That(record!.WasDisabledBefore, Is.False);
        Assert.That((await AuditAsync(editor.Key)).Count(r => r.Action == ArchiveAuditAction.Archived), Is.EqualTo(1));
        Assert.That((await UserService.GetAsync(editor.Key))!.UserState, Is.EqualTo(UserState.Disabled));
    }
}

/// <summary>Counts archive notifications per user key. A DI singleton.</summary>
public sealed class ArchiveNotificationRecorder
{
    public ConcurrentBag<Guid> ArchivingKeys { get; } = new();

    public ConcurrentBag<Guid> ArchivedKeys { get; } = new();

    public sealed class Archiving(ArchiveNotificationRecorder r) : INotificationHandler<UserArchivingNotification>
    {
        public void Handle(UserArchivingNotification n) => r.ArchivingKeys.Add(n.Event.UserKey);
    }

    public sealed class Archived(ArchiveNotificationRecorder r) : INotificationHandler<UserArchivedNotification>
    {
        public void Handle(UserArchivedNotification n) => r.ArchivedKeys.Add(n.Event.UserKey);
    }
}
