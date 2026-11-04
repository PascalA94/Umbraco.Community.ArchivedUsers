using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.AuthorizationStatus;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;

namespace Umbraco.Community.ArchivedUsers.IntegrationTests;

/// <summary>
/// Pins the core behaviour the package depends on (docs/plan.md Task 3, P3; docs/spikes.md S2, S4, U3).
/// <list type="bullet">
/// <item><c>Gating</c>: never edited to match core. A failure triggers the fallback named on the test.</item>
/// <item><c>Tripwire</c>: a failure goes to the security reviewer; never silently updated.</item>
/// <item><c>Recording</c>: may be updated to the observed value, with the observation written in docs/spikes.md.</item>
/// </list>
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
public class CoreContractTests : UmbracoIntegrationTest
{
    private TestUsers Users => new(Services);

    private IUserService UserService => GetRequiredService<IUserService>();

    private UserSavedRecorder Recorder => GetRequiredService<UserSavedRecorder>();

    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        builder.Services.AddSingleton<UserSavedRecorder>();
        builder.AddNotificationHandler<UserSavedNotification, RecordingUserSavedHandler>();
    }

    // ---- S2 (gating) ----

    /// <summary>Fallback on failure: Task 10 builds CoreEnableActionFilter (Task 10 Step 3).</summary>
    [Test]
    [Category("Gating")]
    public async Task EnableAsync_publishes_UserSavedNotification()
    {
        IUser editor = await Users.CreateEditorAsync(state: UserState.Disabled);
        Recorder.Clear();

        UserOperationStatus status = await UserService.EnableAsync(Constants.Security.SuperUserKey, new HashSet<Guid> { editor.Key });

        Assert.That(status, Is.EqualTo(UserOperationStatus.Success));
        var seen = Recorder.Saved.Where(u => u.Key == editor.Key).ToList();
        Assert.That(seen, Has.Count.EqualTo(1));
        Assert.That(seen[0].State, Is.Not.EqualTo(UserState.Disabled));
    }

    /// <summary>Fallback on failure: Task 9 Step 4's UnarchiveInProgress marker (design 7.2 unarchive flow, P3).</summary>
    [Test]
    [Category("Gating")]
    public async Task EnableAsync_inside_outer_scope_defers_UserSaved_until_outer_complete()
    {
        IUser editor = await Users.CreateEditorAsync(state: UserState.Disabled);
        Recorder.Clear();

        using (ICoreScope scope = GetRequiredService<ICoreScopeProvider>().CreateCoreScope())
        {
            await UserService.EnableAsync(Constants.Security.SuperUserKey, new HashSet<Guid> { editor.Key });
            Assert.That(Recorder.Saved.Any(u => u.Key == editor.Key), Is.False, "UserSaved ran before the outer scope completed");
            scope.Complete();
        }

        Assert.That(Recorder.Saved.Count(u => u.Key == editor.Key), Is.EqualTo(1));
    }

    // ---- S4 (gating) ----

    /// <summary>Fallback on failure: Task 7 keeps UserVisibility.CanSee as the only per-user rule; report to security-analyst.</summary>
    [Test]
    [Category("Gating")]
    public async Task AuthorizeAccessAsync_refuses_non_admin_on_admin()
    {
        IUser manager = await Users.CreateUserManagerAsync();
        IUser admin = await Users.CreateAdminAsync();
        IUser editor = await Users.CreateEditorAsync();
        var permissions = GetRequiredService<IUserPermissionService>();

        Assert.That(await permissions.AuthorizeAccessAsync(manager, new[] { admin.Key }), Is.Not.EqualTo(UserAuthorizationStatus.Success));
        Assert.That(await permissions.AuthorizeAccessAsync(manager, new[] { editor.Key }), Is.EqualTo(UserAuthorizationStatus.Success));
        Assert.That(await permissions.AuthorizeAccessAsync(admin, new[] { admin.Key }), Is.EqualTo(UserAuthorizationStatus.Success));
    }

    /// <summary>
    /// AU-02. Fallback on failure (design S4): set a new IUser.SecurityStamp and save through IUserService.
    /// No HttpContext exists in this test: it runs on a service scope, as the package service does from a job.
    /// </summary>
    [Test]
    [Category("Gating")]
    public async Task UpdateSecurityStampAsync_changes_stamp_without_HttpContext()
    {
        IUser editor = await Users.CreateEditorAsync();
        string? before = (await UserService.GetAsync(editor.Key))!.SecurityStamp;

        using (IServiceScope scope = Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<IBackOfficeUserManager>();
            BackOfficeIdentityUser? identity = await manager.FindByIdAsync(editor.Key.ToString());
            Assert.That(identity, Is.Not.Null);
            IdentityResult result = await ((UserManager<BackOfficeIdentityUser>)manager).UpdateSecurityStampAsync(identity!);
            Assert.That(result.Succeeded, Is.True, string.Join(",", result.Errors.Select(e => e.Code)));
        }

        string? after = (await UserService.GetAsync(editor.Key))!.SecurityStamp;
        Assert.That(after, Is.Not.EqualTo(before));
    }

    // ---- S4 (tripwires, AU-01) ----

    /// <summary>
    /// T2. If this ever fails, core added an admin check to DisableAsync. Nothing in the package depends on
    /// core's check, so the package's own check stays either way.
    /// </summary>
    [Test]
    [Category("Tripwire")]
    public async Task DisableAsync_does_not_check_admin()
    {
        IUser manager = await Users.CreateUserManagerAsync();
        IUser admin = await Users.CreateAdminAsync();

        UserOperationStatus status = await UserService.DisableAsync(manager.Key, new HashSet<Guid> { admin.Key });

        Assert.That(status, Is.EqualTo(UserOperationStatus.Success));
        Assert.That((await UserService.GetAsync(admin.Key))!.IsApproved, Is.False);
    }

    /// <summary>T2, the enable side.</summary>
    [Test]
    [Category("Tripwire")]
    public async Task EnableAsync_does_not_check_admin()
    {
        IUser manager = await Users.CreateUserManagerAsync();
        IUser admin = await Users.CreateAsync("Disabled Admin", [Constants.Security.AdminGroupAlias], UserState.Disabled);

        UserOperationStatus status = await UserService.EnableAsync(manager.Key, new HashSet<Guid> { admin.Key });

        Assert.That(status, Is.EqualTo(UserOperationStatus.Success));
        Assert.That((await UserService.GetAsync(admin.Key))!.IsApproved, Is.True);
    }

    /// <summary>Proves the rule UserVisibility mirrors: a non-admin never sees admins or the superuser.</summary>
    [Test]
    [Category("Tripwire")]
    public async Task FilterAsync_hides_admins_from_non_admin()
    {
        IUser manager = await Users.CreateUserManagerAsync();
        IUser admin = await Users.CreateAdminAsync();

        Attempt<PagedModel<IUser>, UserOperationStatus> result = await UserService.FilterAsync(manager.Key, new UserFilter(), 0, 1000);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Result.Items.Select(u => u.Key), Does.Not.Contain(admin.Key));
        Assert.That(result.Result.Items.Select(u => u.Key), Does.Not.Contain(Constants.Security.SuperUserKey));
    }

    /// <summary>
    /// The list filter matches core's action by name. v17 line: core's live action is Filter (release-17.8.0); if a
    /// 17.x minor adds Filter2 as on v18, hiding stops, which is core behaviour, not a leak. This fails loudly so the
    /// filter is updated (docs/design.md 11).
    /// </summary>
    [Test]
    [Category("Tripwire")]
    public void Filter_user_action_name_is_Filter()
    {
        Type controller = typeof(Umbraco.Cms.Api.Management.Controllers.User.Filter.FilterUserFilterController);
        System.Reflection.MethodInfo? live = controller.GetMethod("Filter");
        Assert.That(live, Is.Not.Null);
        Assert.That(live!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpGetAttribute), false), Is.Not.Empty);
        Assert.That(live.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.NonActionAttribute), false), Is.Empty);
        Assert.That(controller.GetMethod("Filter2"), Is.Null);
        Assert.That(ArchivedUsers.Web.Filters.ArchivedUserListFilter.BoundActionName, Is.EqualTo("Filter"));
    }

    // ---- S4 / U3 (recording) ----

    [Test]
    [Category("Recording")]
    public async Task DisableAsync_already_disabled_user()
    {
        IUser editor = await Users.CreateEditorAsync(state: UserState.Disabled);
        Assert.That(await UserService.DisableAsync(Constants.Security.SuperUserKey, new HashSet<Guid> { editor.Key }), Is.EqualTo(UserOperationStatus.Success));
    }

    [Test]
    [Category("Recording")]
    public async Task DisableAsync_self()
    {
        IUser admin = await Users.CreateAdminAsync();
        Assert.That(await UserService.DisableAsync(admin.Key, new HashSet<Guid> { admin.Key }), Is.EqualTo(UserOperationStatus.CannotDisableSelf));
    }

    [Test]
    [Category("Recording")]
    public async Task DisableAsync_invited_user()
    {
        IUser invited = await Users.CreateEditorAsync(state: UserState.Invited, hasLoggedIn: false);
        Assert.That(invited.UserState, Is.EqualTo(UserState.Invited));
        Assert.That(await UserService.DisableAsync(Constants.Security.SuperUserKey, new HashSet<Guid> { invited.Key }), Is.EqualTo(UserOperationStatus.CannotDisableInvitedUser));
    }

    [Test]
    [Category("Recording")]
    public async Task DisableAsync_with_SuperUserKey_as_performer_succeeds_for_every_user_type()
    {
        IUser editor = await Users.CreateEditorAsync();
        IUser admin = await Users.CreateAdminAsync();
        IUser manager = await Users.CreateUserManagerAsync();

        foreach (IUser target in new[] { editor, admin, manager })
        {
            Assert.That(
                await UserService.DisableAsync(Constants.Security.SuperUserKey, new HashSet<Guid> { target.Key }),
                Is.EqualTo(UserOperationStatus.Success),
                target.Name);
        }
    }

    /// <summary>U3. Records whether core's disable rotates the stamp; the package rotates either way (AU-02).</summary>
    [Test]
    [Category("Recording")]
    public async Task DisableAsync_does_not_rotate_security_stamp()
    {
        IUser editor = await Users.CreateEditorAsync();
        string? before = (await UserService.GetAsync(editor.Key))!.SecurityStamp;

        await UserService.DisableAsync(Constants.Security.SuperUserKey, new HashSet<Guid> { editor.Key });

        string? after = (await UserService.GetAsync(editor.Key))!.SecurityStamp;
        Assert.That(after, Is.EqualTo(before), "Observed on 18.3.0: DisableAsync does not rotate the stamp (docs/spikes.md U3)");
    }

    /// <summary>AU-07: the core audit call the package uses for its second record.</summary>
    [Test]
    [Category("Recording")]
    public async Task AuditService_write_is_readable_back()
    {
        IUser admin = await Users.CreateAdminAsync();
        IUser editor = await Users.CreateEditorAsync();
        var audit = GetRequiredService<IAuditService>();

        Attempt<AuditLogOperationStatus> written = await audit.AddAsync(AuditType.Custom, admin.Key, editor.Id, "User", "ArchivedUsers: Archived (Manual)");

        Assert.That(written.Success, Is.True, written.Result.ToString());
        PagedModel<IAuditItem> items = await audit.GetItemsByEntityAsync(editor.Id, 0, 10);
        Assert.That(items.Items.Any(i => i.Comment == "ArchivedUsers: Archived (Manual)" && i.AuditType == AuditType.Custom && i.EntityType == "User"), Is.True);
    }
}

/// <summary>Records every saved user (key and state at publish time). A DI singleton, never a static.</summary>
public sealed class UserSavedRecorder
{
    private readonly ConcurrentQueue<(Guid Key, UserState State)> _saved = new();

    public IReadOnlyList<(Guid Key, UserState State)> Saved => _saved.ToList();

    public void Add(IUser user) => _saved.Enqueue((user.Key, user.UserState));

    public void Clear() => _saved.Clear();
}

public sealed class RecordingUserSavedHandler(UserSavedRecorder recorder) : INotificationHandler<UserSavedNotification>
{
    public void Handle(UserSavedNotification notification)
    {
        foreach (IUser user in notification.SavedEntities)
        {
            recorder.Add(user);
        }
    }
}
