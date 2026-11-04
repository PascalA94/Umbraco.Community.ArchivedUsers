using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.ArchivedUsers.Core.Handlers;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Services;

namespace Umbraco.Community.ArchivedUsers.IntegrationTests;

[TestFixture]
public class ReconcileTests : ArchiveTableTestBase
{
    /// <summary>T3, AU-03.</summary>
    [Test]
    public async Task Record_of_enabled_user_is_removed_with_Reconcile_audit()
    {
        IUser editor = await Users.CreateEditorAsync();
        await InsertRecordAsync(editor.Key);

        int removed = await Service.ReconcileAsync(CancellationToken.None);

        Assert.That(removed, Is.EqualTo(1));
        Assert.That(await RecordAsync(editor.Key), Is.Null);
        ArchiveAuditEntry row = (await AuditAsync(editor.Key)).Single();
        Assert.That(row.Action, Is.EqualTo(ArchiveAuditAction.Unarchived));
        Assert.That(row.Source, Is.EqualTo(ArchiveSource.Reconcile));
        Assert.That(row.PerformedByUserKey, Is.Null);
    }

    [Test]
    public async Task Record_of_deleted_user_is_removed_with_Reconcile_audit()
    {
        var gone = Guid.NewGuid();
        await InsertRecordAsync(gone);

        Assert.That(await Service.ReconcileAsync(CancellationToken.None), Is.EqualTo(1));

        Assert.That(await RecordAsync(gone), Is.Null);
        Assert.That((await AuditAsync(gone)).Single().Source, Is.EqualTo(ArchiveSource.Reconcile));
    }

    [Test]
    public async Task Record_of_disabled_user_is_kept()
    {
        IUser editor = await Users.CreateEditorAsync(state: UserState.Disabled);
        await InsertRecordAsync(editor.Key);

        Assert.That(await Service.ReconcileAsync(CancellationToken.None), Is.EqualTo(0));
        Assert.That(await RecordAsync(editor.Key), Is.Not.Null);
        Assert.That(await AuditAsync(editor.Key), Is.Empty);
    }

    /// <summary>AU-14b: only the call whose delete removed the row writes the audit row.</summary>
    [Test]
    public async Task Second_concurrent_reconcile_writes_no_second_row()
    {
        IUser editor = await Users.CreateEditorAsync();
        await InsertRecordAsync(editor.Key);

        async Task<int> ReconcileInOwnScope()
        {
            using IServiceScope scope = Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IArchivedUserService>().ReconcileAsync(CancellationToken.None);
        }

        int[] removed = await Task.WhenAll(Task.Run(ReconcileInOwnScope), Task.Run(ReconcileInOwnScope));

        Assert.That(removed.Sum(), Is.EqualTo(1));
        Assert.That((await AuditAsync(editor.Key)).Count(r => r.Source == ArchiveSource.Reconcile), Is.EqualTo(1));
    }

    [Test]
    public async Task Startup_handler_reconciles_stale_records()
    {
        IUser editor = await Users.CreateEditorAsync();
        await InsertRecordAsync(editor.Key);

        await GetRequiredService<ReconcileOnStartupHandler>().HandleAsync(new UmbracoApplicationStartedNotification(false), CancellationToken.None);

        Assert.That(await RecordAsync(editor.Key), Is.Null);
    }
}

/// <summary>Startup must never fail because of housekeeping (F7a): here the package tables do not exist yet.</summary>
[TestFixture]
public class ReconcileStartupFailureTests : ArchivedUsersIntegrationTestBase
{
    [Test]
    public void Startup_handler_reconciles_and_swallows_exceptions()
    {
        var handler = GetRequiredService<ReconcileOnStartupHandler>();

        Assert.DoesNotThrowAsync(() => handler.HandleAsync(new UmbracoApplicationStartedNotification(false), CancellationToken.None));
    }
}
