using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Community.ArchivedUsers.Core.Configuration;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Notifications;

namespace Umbraco.Community.ArchivedUsers.IntegrationTests.Api;

[TestFixture]
public class ArchivedUsersApiTests : ArchivedUsersApiTestBase
{
    private async Task<(IUser Admin, HttpClient Client)> AdminAsync()
    {
        IUser admin = await Users.CreateAdminAsync();
        return (admin, await CreateAuthenticatedClientAsync(admin));
    }

    // ---- Validation ----

    [Test]
    public async Task Empty_userIds_returns_400()
    {
        (_, HttpClient client) = await AdminAsync();
        Assert.That((await client.PostAsync(Root + "/archive", Json(new { userIds = Array.Empty<Guid>() }))).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await client.PostAsync(Root + "/unarchive", Json(new { userIds = Array.Empty<Guid>() }))).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Five_hundred_and_one_userIds_returns_400_problem_details()
    {
        (_, HttpClient client) = await AdminAsync();

        HttpResponseMessage response = await client.PostAsync(Root + "/archive", Json(new { userIds = Enumerable.Range(0, 501).Select(_ => Guid.NewGuid()).ToArray() }));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
    }

    [Test]
    public async Task Reason_over_500_returns_400()
    {
        (_, HttpClient client) = await AdminAsync();
        IUser editor = await Users.CreateEditorAsync();

        HttpResponseMessage response = await client.PostAsync(Root + "/archive", Json(new { userIds = new[] { editor.Key }, reason = new string('x', 501) }));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await IsApprovedAsync(editor.Key), Is.True);
    }

    [Test]
    public async Task Duplicate_ids_are_processed_once()
    {
        (_, HttpClient client) = await AdminAsync();
        IUser editor = await Users.CreateEditorAsync();

        JsonElement results = await ReadJsonAsync(await client.PostAsync(Root + "/archive", Json(new { userIds = new[] { editor.Key, editor.Key } })));

        Assert.That(results.GetArrayLength(), Is.EqualTo(1));
        Assert.That((await RecordAsync(editor.Key))!.Source, Is.EqualTo(ArchiveSource.Manual), "one distinct id counts as a single archive");
    }

    /// <summary>T10, AU-11.</summary>
    [Test]
    public async Task Enable_true_with_setting_off_returns_400_and_user_stays_disabled()
    {
        (_, HttpClient client) = await AdminAsync();
        IUser editor = await ArchivedAsync(client, await Users.CreateEditorAsync());

        HttpResponseMessage response = await client.PostAsync(Root + "/unarchive", Json(new { userIds = new[] { editor.Key }, enable = true }));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("Unarchive cannot enable users on this site"));
        Assert.That(await IsApprovedAsync(editor.Key), Is.False);
        Assert.That(await RecordAsync(editor.Key), Is.Not.Null);
    }

    // ---- Paging and ordering (P14) ----

    [Test]
    public async Task List_with_unknown_orderBy_returns_400()
    {
        (_, HttpClient client) = await AdminAsync();
        Assert.That((await client.GetAsync(Root + "?orderBy=email")).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task List_with_take_zero_returns_no_items_and_total()
    {
        (_, HttpClient client) = await AdminAsync();
        for (var i = 0; i < 3; i++)
        {
            await ArchivedAsync(client, await Users.CreateEditorAsync($"Editor {i}"));
        }

        JsonElement list = await ReadJsonAsync(await client.GetAsync(Root + "?take=0"));

        Assert.That(list.GetProperty("items").GetArrayLength(), Is.EqualTo(0));
        Assert.That(list.GetProperty("total").GetInt32(), Is.EqualTo(3));
    }

    [Test]
    public async Task List_with_take_1001_returns_400()
    {
        (_, HttpClient client) = await AdminAsync();
        Assert.That((await client.GetAsync(Root + "?take=1001")).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task List_with_negative_skip_returns_400()
    {
        (_, HttpClient client) = await AdminAsync();
        Assert.That((await client.GetAsync(Root + "?skip=-1")).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task List_sorts_by_name_and_filters()
    {
        (_, HttpClient client) = await AdminAsync();
        await ArchivedAsync(client, await Users.CreateEditorAsync("Bravo"));
        await ArchivedAsync(client, await Users.CreateEditorAsync("Alpha"));
        await ArchivedAsync(client, await Users.CreateEditorAsync("Charlie"));

        JsonElement sorted = await ReadJsonAsync(await client.GetAsync(Root + "?orderBy=name&orderDirection=Ascending"));
        JsonElement filtered = await ReadJsonAsync(await client.GetAsync(Root + "?filter=CHAR"));

        Assert.That(sorted.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("user").GetProperty("name").GetString()), Is.EqualTo(new[] { "Alpha", "Bravo", "Charlie" }));
        Assert.That(filtered.GetProperty("total").GetInt32(), Is.EqualTo(1));
    }

    // ---- archivedBy shapes (P14, Review Focus 5) ----

    [Test]
    public async Task List_item_archived_by_system_has_null_archivedBy()
    {
        (_, HttpClient client) = await AdminAsync();
        IUser editor = await Users.CreateEditorAsync(state: UserState.Disabled);
        await InsertRecordAsync(editor.Key, archivedBy: null);

        JsonElement item = (await ReadJsonAsync(await client.GetAsync(Root))).GetProperty("items")[0];

        Assert.That(item.GetProperty("archivedBy").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task List_item_archived_by_deleted_admin_has_id_and_null_name()
    {
        (IUser admin, HttpClient client) = await AdminAsync();
        IUser x = await Users.CreateAdminAsync("Admin X");
        x.LastLoginDate = null;
        UserService.Save(x);
        IUser editor = await Users.CreateEditorAsync(state: UserState.Disabled);
        await InsertRecordAsync(editor.Key, archivedBy: x.Key);
        Assert.That(await UserService.DeleteAsync(admin.Key, x.Key), Is.EqualTo(Umbraco.Cms.Core.Services.OperationStatus.UserOperationStatus.Success));

        JsonElement archivedBy = (await ReadJsonAsync(await client.GetAsync(Root))).GetProperty("items")[0].GetProperty("archivedBy");

        Assert.That(archivedBy.GetProperty("id").GetGuid(), Is.EqualTo(x.Key));
        Assert.That(archivedBy.GetProperty("name").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    // ---- Stale record (T3, AU-03) ----

    [Test]
    public async Task Get_by_id_of_stale_record_returns_stale_true()
    {
        (_, HttpClient client) = await AdminAsync();
        IUser editor = await Users.CreateEditorAsync();
        await InsertRecordAsync(editor.Key);

        JsonElement item = await ReadJsonAsync(await client.GetAsync(Root + "/" + editor.Key));

        Assert.That(item.GetProperty("stale").GetBoolean(), Is.True);
        Assert.That(await RecordAsync(editor.Key), Is.Not.Null, "GET never changes state (AU-14)");
    }

    [Test]
    public async Task List_flags_stale_record()
    {
        (_, HttpClient client) = await AdminAsync();
        IUser editor = await Users.CreateEditorAsync();
        await InsertRecordAsync(editor.Key);

        JsonElement item = (await ReadJsonAsync(await client.GetAsync(Root))).GetProperty("items")[0];

        Assert.That(item.GetProperty("stale").GetBoolean(), Is.True);
    }

    // ---- Mixed bulk (P9) and double submit (P6) ----

    [Test]
    public async Task Non_admin_mixed_bulk_returns_per_user_statuses()
    {
        IUser manager = await Users.CreateUserManagerAsync();
        HttpClient client = await CreateAuthenticatedClientAsync(manager);
        IUser invited = await Users.CreateEditorAsync("Invited", UserState.Invited, hasLoggedIn: false);
        IUser admin = await Users.CreateAdminAsync();
        IUser editor = await Users.CreateEditorAsync();

        JsonElement results = await ReadJsonAsync(await client.PostAsync(Root + "/archive", Json(new { userIds = new[] { manager.Key, invited.Key, admin.Key, editor.Key } })));

        Assert.That(results.EnumerateArray().Select(r => r.GetProperty("status").GetString()), Is.EqualTo(new[] { "Self", "Invited", "NotPermitted", "Archived" }));
        Assert.That(await IsApprovedAsync(admin.Key), Is.True);
        Assert.That(await RecordAsync(editor.Key), Is.Not.Null);
        Assert.That(await RecordAsync(admin.Key), Is.Null);
    }

    [Test]
    public async Task Two_parallel_archive_posts_for_one_user_give_one_record()
    {
        (_, HttpClient client) = await AdminAsync();
        IUser editor = await Users.CreateEditorAsync();

        HttpResponseMessage[] responses = await Task.WhenAll(
            client.PostAsync(Root + "/archive", Json(new { userIds = new[] { editor.Key } })),
            client.PostAsync(Root + "/archive", Json(new { userIds = new[] { editor.Key } })));

        Assert.That(responses.Select(r => r.StatusCode), Is.All.EqualTo(HttpStatusCode.OK));
        var statuses = new List<string>();
        foreach (HttpResponseMessage r in responses)
        {
            statuses.Add((await ReadJsonAsync(r))[0].GetProperty("status").GetString()!);
        }

        Assert.That(statuses, Is.EquivalentTo(new[] { "Archived", "AlreadyArchived" }));
        Assert.That((await AuditAsync(editor.Key)).Count(a => a.Action == ArchiveAuditAction.Archived), Is.EqualTo(1));
    }

    /// <summary>P11, Review Focus 2: stored and returned byte for byte.</summary>
    [Test]
    public async Task Reason_with_emoji_cjk_and_markup_round_trips_verbatim()
    {
        const string reason = "Left 🎉 Zoë 李 <b>x</b> & \"q\"";
        (_, HttpClient client) = await AdminAsync();
        IUser editor = await Users.CreateEditorAsync();

        await PostStatusesAsync(client, "archive", new { userIds = new[] { editor.Key }, reason });
        JsonElement item = await ReadJsonAsync(await client.GetAsync(Root + "/" + editor.Key));

        Assert.That(item.GetProperty("reason").GetString(), Is.EqualTo(reason));
    }

    // ---- Behaviour ----

    [Test]
    public async Task Archive_one_user_returns_Archived_and_source_Manual()
    {
        (IUser admin, HttpClient client) = await AdminAsync();
        IUser editor = await Users.CreateEditorAsync();

        Dictionary<Guid, string> result = await PostStatusesAsync(client, "archive", new { userIds = new[] { editor.Key }, reason = "  Left 24 Oct, SUP-123  " });

        Assert.That(result[editor.Key], Is.EqualTo("Archived"));
        ArchivedUserRecord record = (await RecordAsync(editor.Key))!;
        Assert.That(record.Source, Is.EqualTo(ArchiveSource.Manual));
        Assert.That(record.Reason, Is.EqualTo("Left 24 Oct, SUP-123"), "trimmed on the server");
        Assert.That(await IsApprovedAsync(editor.Key), Is.False);
    }

    [Test]
    public async Task Archive_two_users_records_source_Bulk()
    {
        (_, HttpClient client) = await AdminAsync();
        IUser a = await Users.CreateEditorAsync("A"), b = await Users.CreateEditorAsync("B");

        await PostStatusesAsync(client, "archive", new { userIds = new[] { a.Key, b.Key } });

        Assert.That((await RecordAsync(a.Key))!.Source, Is.EqualTo(ArchiveSource.Bulk));
        Assert.That((await RecordAsync(b.Key))!.Source, Is.EqualTo(ArchiveSource.Bulk));
    }

    [Test]
    public async Task Get_by_id_returns_archive_status()
    {
        (IUser admin, HttpClient client) = await AdminAsync();
        IUser editor = await ArchivedAsync(client, await Users.CreateEditorAsync("Jo"));

        JsonElement item = await ReadJsonAsync(await client.GetAsync(Root + "/" + editor.Key));
        HttpResponseMessage notArchived = await client.GetAsync(Root + "/" + (await Users.CreateEditorAsync()).Key);

        Assert.That(item.GetProperty("user").GetProperty("id").GetGuid(), Is.EqualTo(editor.Key));
        Assert.That(item.GetProperty("user").GetProperty("name").GetString(), Is.EqualTo("Jo"));
        Assert.That(item.GetProperty("archivedBy").GetProperty("id").GetGuid(), Is.EqualTo(admin.Key));
        Assert.That(item.GetProperty("source").GetString(), Is.EqualTo("Manual"));
        Assert.That(item.GetProperty("stale").GetBoolean(), Is.False);
        Assert.That(notArchived.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Unarchive_returns_Unarchived_and_user_stays_disabled()
    {
        (_, HttpClient client) = await AdminAsync();
        IUser editor = await ArchivedAsync(client, await Users.CreateEditorAsync());

        Dictionary<Guid, string> result = await PostStatusesAsync(client, "unarchive", new { userIds = new[] { editor.Key } });

        Assert.That(result[editor.Key], Is.EqualTo("Unarchived"));
        Assert.That(await RecordAsync(editor.Key), Is.Null);
        Assert.That(await IsApprovedAsync(editor.Key), Is.False);
    }

    [Test]
    public async Task Clean_up_preview_and_run()
    {
        (_, HttpClient client) = await AdminAsync();
        IUser former = await Users.CreateEditorAsync("Former", UserState.Disabled);

        JsonElement preview = await ReadJsonAsync(await client.GetAsync(Root + "/clean-up/preview"));
        JsonElement results = await ReadJsonAsync(await client.PostAsync(Root + "/clean-up", Json(new { reason = "Tidy-up" })));
        JsonElement after = await ReadJsonAsync(await client.GetAsync(Root + "/clean-up/preview"));

        Assert.That(preview.GetProperty("count").GetInt32(), Is.EqualTo(1));
        Assert.That(preview.GetProperty("sample")[0].GetProperty("name").GetString(), Is.EqualTo("Former"));
        Assert.That(results[0].GetProperty("status").GetString(), Is.EqualTo("Archived"));
        Assert.That((await RecordAsync(former.Key))!.Source, Is.EqualTo(ArchiveSource.CleanUp));
        Assert.That(after.GetProperty("count").GetInt32(), Is.EqualTo(0));
    }

    [Test]
    public async Task Config_returns_bound_settings()
    {
        (_, HttpClient client) = await AdminAsync();

        JsonElement config = await ReadJsonAsync(await client.GetAsync(Root + "/config"));

        Assert.That(config.GetProperty("hideArchivedFromUserList").GetBoolean(), Is.True);
        Assert.That(config.GetProperty("requireReason").GetBoolean(), Is.False);
        Assert.That(config.GetProperty("unarchiveEnablesUser").GetBoolean(), Is.False);
        Assert.That(config.GetProperty("adminGroupId").GetGuid(), Is.EqualTo(Constants.Security.AdminGroupKey));
        Assert.That(config.GetProperty("superUserId").GetGuid(), Is.EqualTo(Constants.Security.SuperUserKey));
    }

    /// <summary>The package's own OpenAPI document (docs/spikes.md S6).</summary>
    [Test]
    public async Task Package_openapi_document_lists_seven_operations_under_Archived_Users()
    {
        HttpResponseMessage response = await CreateAnonymousClient().GetAsync("/umbraco/openapi/archived-users.json");
        JsonElement doc = await ReadJsonAsync(response);

        var operations = doc.GetProperty("paths").EnumerateObject()
            .SelectMany(p => p.Value.EnumerateObject().Select(m => (Path: p.Name, Method: m.Name, Op: m.Value)))
            .ToList();
        Assert.That(operations, Has.Count.EqualTo(7));
        Assert.That(operations.All(o => o.Op.GetProperty("tags").EnumerateArray().Any(t => t.GetString() == "Archived Users")), Is.True);
        Assert.That(operations.All(o => o.Path.StartsWith("/umbraco/management/api/v1/archived-users", StringComparison.Ordinal)), Is.True);
    }
}

[TestFixture]
public class ArchivedUsersRequireReasonApiTests : ArchivedUsersApiTestBase
{
    protected override void CustomTestSetup(IUmbracoBuilder builder)
        => builder.Services.PostConfigure<ArchivedUsersSettings>(s => s.RequireReason = true);

    [Test]
    public async Task RequireReason_with_whitespace_reason_returns_400()
    {
        HttpClient client = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());
        IUser editor = await Users.CreateEditorAsync();

        HttpResponseMessage archive = await client.PostAsync(Root + "/archive", Json(new { userIds = new[] { editor.Key }, reason = "   " }));
        HttpResponseMessage cleanUp = await client.PostAsync(Root + "/clean-up", Json(new { reason = "" }));

        Assert.That(archive.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(cleanUp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await IsApprovedAsync(editor.Key), Is.True);
    }
}

[TestFixture]
public class ArchivedUsersCancelApiTests : ArchivedUsersApiTestBase
{
    protected override void CustomTestSetup(IUmbracoBuilder builder)
        => builder.AddNotificationHandler<UserArchivingNotification, CancelEveryArchive>();

    [Test]
    public async Task Cancelled_archive_returns_message()
    {
        HttpClient client = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());
        IUser editor = await Users.CreateEditorAsync();

        JsonElement result = (await ReadJsonAsync(await client.PostAsync(Root + "/archive", Json(new { userIds = new[] { editor.Key } }))))[0];

        Assert.That(result.GetProperty("status").GetString(), Is.EqualTo("Cancelled"));
        Assert.That(result.GetProperty("message").GetString(), Is.EqualTo("Locked by HR"));
        Assert.That(await IsApprovedAsync(editor.Key), Is.True);
    }

    public sealed class CancelEveryArchive : INotificationHandler<UserArchivingNotification>
    {
        public void Handle(UserArchivingNotification notification)
            => notification.CancelOperation(new EventMessage("Policy", "Locked by HR", EventMessageType.Error));
    }
}
