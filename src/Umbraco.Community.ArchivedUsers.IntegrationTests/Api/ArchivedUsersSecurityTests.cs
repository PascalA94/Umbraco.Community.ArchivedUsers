using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.ArchivedUsers.Core.Configuration;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Persistence;

namespace Umbraco.Community.ArchivedUsers.IntegrationTests.Api;

/// <summary>Shared helpers for package API tests.</summary>
public abstract class ArchivedUsersApiTestBase : ApiTestBase
{
    protected IUserService UserService => Services.GetRequiredService<IUserService>();

    private protected async Task<T> InScope<T>(Func<IArchivedUserRepository, Task<T>> work)
    {
        using IServiceScope di = Services.CreateScope();
        using ICoreScope scope = di.ServiceProvider.GetRequiredService<ICoreScopeProvider>().CreateCoreScope();
        T result = await work(di.ServiceProvider.GetRequiredService<IArchivedUserRepository>());
        scope.Complete();
        return result;
    }

    protected Task<ArchivedUserRecord?> RecordAsync(Guid key) => InScope(r => r.GetAsync(key));

    protected Task<IReadOnlyList<ArchiveAuditEntry>> AuditAsync(Guid key) => InScope(r => r.GetAuditForUserAsync(key));

    protected Task InsertRecordAsync(Guid key, Guid? archivedBy = null, bool wasDisabledBefore = false)
        => InScope(r => r.TryInsertAsync(new ArchivedUserRecord(key, DateTime.UtcNow, archivedBy, ArchiveSource.Manual, null, wasDisabledBefore)));

    protected async Task<bool> IsApprovedAsync(Guid key) => (await UserService.GetAsync(key))!.IsApproved;

    protected async Task<IUser> ArchivedAsync(HttpClient admin, IUser target)
    {
        Dictionary<Guid, string> result = await PostStatusesAsync(admin, "archive", new { userIds = new[] { target.Key } });
        Assert.That(result[target.Key], Is.EqualTo("Archived"));
        return target;
    }
}

[TestFixture]
public class ArchivedUsersSecurityTests : ArchivedUsersApiTestBase
{
    private static HttpRequestMessage Request(HttpMethod method, string path)
        => new(method, path) { Content = method == HttpMethod.Post ? new StringContent("{\"userIds\":[\"" + Guid.NewGuid() + "\"]}", Encoding.UTF8, "application/json") : null };

    /// <summary>T14.</summary>
    [Test]
    public async Task Unauthenticated_gets_401_on_every_route()
    {
        HttpClient anonymous = CreateAnonymousClient();
        foreach ((HttpMethod method, string path) in AllRoutes())
        {
            HttpResponseMessage response = await anonymous.SendAsync(Request(method, path));
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), $"{method} {path}");
        }
    }

    /// <summary>T14.</summary>
    [Test]
    public async Task User_without_Users_section_gets_403_on_every_route()
    {
        HttpClient editor = await CreateAuthenticatedClientAsync(await Users.CreateEditorAsync());
        foreach ((HttpMethod method, string path) in AllRoutes())
        {
            HttpResponseMessage response = await editor.SendAsync(Request(method, path));
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), $"{method} {path}");
        }
    }

    [Test]
    public async Task Non_admin_get_admin_by_id_returns_404()
    {
        IUser admin = await Users.CreateAdminAsync(), archivedAdmin = await Users.CreateAdminAsync("Archived admin");
        await ArchivedAsync(await CreateAuthenticatedClientAsync(admin), archivedAdmin);
        HttpClient manager = await CreateAuthenticatedClientAsync(await Users.CreateUserManagerAsync());

        HttpResponseMessage response = await manager.GetAsync(Root + "/" + archivedAdmin.Key);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Non_admin_list_excludes_archived_admins()
    {
        IUser admin = await Users.CreateAdminAsync();
        HttpClient adminClient = await CreateAuthenticatedClientAsync(admin);
        IUser archivedAdmin = await ArchivedAsync(adminClient, await Users.CreateAdminAsync("Archived admin"));
        IUser archivedEditor = await ArchivedAsync(adminClient, await Users.CreateEditorAsync("Archived editor"));
        HttpClient manager = await CreateAuthenticatedClientAsync(await Users.CreateUserManagerAsync());

        JsonElement list = await ReadJsonAsync(await manager.GetAsync(Root));

        var ids = list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("user").GetProperty("id").GetGuid()).ToList();
        Assert.That(ids, Does.Contain(archivedEditor.Key).And.Not.Contain(archivedAdmin.Key));
        Assert.That(list.GetProperty("total").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task Non_admin_preview_excludes_admins()
    {
        IUser disabledAdmin = await Users.CreateAsync("Former admin", [Constants.Security.AdminGroupAlias], UserState.Disabled);
        HttpClient manager = await CreateAuthenticatedClientAsync(await Users.CreateUserManagerAsync());

        JsonElement preview = await ReadJsonAsync(await manager.GetAsync(Root + "/clean-up/preview"));

        Assert.That(preview.GetProperty("sample").EnumerateArray().Select(s => s.GetProperty("id").GetGuid()), Does.Not.Contain(disabledAdmin.Key));
    }

    [Test]
    public async Task Result_for_unseen_user_carries_no_name_or_email()
    {
        IUser admin = await Users.CreateAdminAsync("Very Secret Admin Name");
        HttpClient manager = await CreateAuthenticatedClientAsync(await Users.CreateUserManagerAsync());

        string body = await (await manager.PostAsync(Root + "/archive", Json(new { userIds = new[] { admin.Key } }))).Content.ReadAsStringAsync();

        Assert.That(body, Does.Not.Contain("Very Secret Admin Name").And.Not.Contain(admin.Email));
        Assert.That(body, Does.Contain("NotPermitted"));
    }

    // ---- T1 (AU-01): every write route, reading the target back ----

    [Test]
    public async Task Non_admin_archive_admin_returns_NotPermitted_and_admin_stays_enabled()
    {
        IUser admin = await Users.CreateAdminAsync();
        HttpClient manager = await CreateAuthenticatedClientAsync(await Users.CreateUserManagerAsync());

        Dictionary<Guid, string> result = await PostStatusesAsync(manager, "archive", new { userIds = new[] { admin.Key } });

        Assert.That(result[admin.Key], Is.EqualTo("NotPermitted"));
        Assert.That(await IsApprovedAsync(admin.Key), Is.True);
    }

    [Test]
    public async Task Non_admin_unarchive_archived_admin_returns_NotPermitted_and_record_kept()
    {
        IUser admin = await Users.CreateAdminAsync();
        IUser archivedAdmin = await ArchivedAsync(await CreateAuthenticatedClientAsync(admin), await Users.CreateAdminAsync("Archived admin"));
        HttpClient manager = await CreateAuthenticatedClientAsync(await Users.CreateUserManagerAsync());

        Dictionary<Guid, string> result = await PostStatusesAsync(manager, "unarchive", new { userIds = new[] { archivedAdmin.Key } });

        Assert.That(result[archivedAdmin.Key], Is.EqualTo("NotPermitted"));
        Assert.That(await RecordAsync(archivedAdmin.Key), Is.Not.Null);
    }

    [Test]
    public async Task Non_admin_clean_up_does_not_archive_disabled_admin()
    {
        IUser disabledAdmin = await Users.CreateAsync("Former admin", [Constants.Security.AdminGroupAlias], UserState.Disabled);
        IUser disabledEditor = await Users.CreateEditorAsync("Former editor", UserState.Disabled);
        HttpClient manager = await CreateAuthenticatedClientAsync(await Users.CreateUserManagerAsync());

        await ReadJsonAsync(await manager.PostAsync(Root + "/clean-up", Json(new { })));

        Assert.That(await RecordAsync(disabledAdmin.Key), Is.Null);
        Assert.That(await RecordAsync(disabledEditor.Key), Is.Not.Null, "clean-up did run (review BS-6)");
    }

    [Test]
    public async Task Admin_archive_superuser_returns_NotPermitted_and_superuser_stays_enabled()
    {
        HttpClient admin = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());

        Dictionary<Guid, string> result = await PostStatusesAsync(admin, "archive", new { userIds = new[] { Constants.Security.SuperUserKey } });

        Assert.That(result[Constants.Security.SuperUserKey], Is.EqualTo("NotPermitted"));
        Assert.That(await IsApprovedAsync(Constants.Security.SuperUserKey), Is.True);
    }

    // ---- T13 (AU-16) ----

    [Test]
    public async Task Non_admin_gets_NotPermitted_for_unknown_key_and_for_admin_key_alike()
    {
        IUser admin = await Users.CreateAdminAsync();
        var unknown = Guid.NewGuid();
        HttpClient manager = await CreateAuthenticatedClientAsync(await Users.CreateUserManagerAsync());

        Dictionary<Guid, string> result = await PostStatusesAsync(manager, "archive", new { userIds = new[] { unknown, admin.Key } });

        Assert.That(result[unknown], Is.EqualTo("NotPermitted"));
        Assert.That(result[admin.Key], Is.EqualTo("NotPermitted"));
    }

    [Test]
    public async Task Admin_gets_NotFound_for_unknown_key()
    {
        var unknown = Guid.NewGuid();
        HttpClient admin = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());

        Assert.That((await PostStatusesAsync(admin, "archive", new { userIds = new[] { unknown } }))[unknown], Is.EqualTo("NotFound"));
    }

    // ---- T12 (AU-12, AU-14) ----

    [Test]
    public async Task Body_fields_source_performerKey_archivedUtc_are_ignored()
    {
        IUser admin = await Users.CreateAdminAsync(), editor = await Users.CreateEditorAsync();
        HttpClient client = await CreateAuthenticatedClientAsync(admin);
        DateTime before = DateTime.UtcNow.AddMinutes(-1);

        await PostStatusesAsync(client, "archive", new
        {
            userIds = new[] { editor.Key },
            source = "Policy",
            performerKey = Guid.NewGuid(),
            archivedUtc = "2001-01-01T00:00:00Z",
        });

        ArchivedUserRecord record = (await RecordAsync(editor.Key))!;
        Assert.That(record.Source, Is.EqualTo(ArchiveSource.Manual));
        Assert.That(record.ArchivedByUserKey, Is.EqualTo(admin.Key));
        Assert.That(record.ArchivedUtc, Is.GreaterThan(before));
    }

    [Test]
    public async Task Form_encoded_post_returns_415()
    {
        HttpClient admin = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());
        foreach (string action in new[] { "archive", "unarchive", "clean-up" })
        {
            var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["userIds"] = Guid.NewGuid().ToString() });
            HttpResponseMessage response = await admin.PostAsync(Root + "/" + action, form);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnsupportedMediaType), action);
        }
    }

#if UMBRACO_HAS_API_USERS
    /// <summary>T5: an archived API user can no longer get a client-credentials token.</summary>
    [Test]
    public async Task Archived_api_user_cannot_get_client_credentials_token()
    {
        const string clientId = "umbraco-back-office-archived-users-t5";
        const string secret = "t5-secret-0123456789-abcdefghij"; // gitleaks:allow (test-only client secret for a throwaway test database)
        IUser apiUser = await Users.CreateApiUserAsync("T5 API user", clientId, secret);
        var tokenRequest = () => CreateAnonymousClient().PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials", ["client_id"] = clientId, ["client_secret"] = secret,
        }));
        Assert.That((await tokenRequest()).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await ArchivedAsync(await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync()), apiUser);

        Assert.That((await tokenRequest()).StatusCode, Is.Not.EqualTo(HttpStatusCode.OK));
    }
#endif
}

/// <summary>T1 and T10 with UnarchiveEnablesUser on.</summary>
[TestFixture]
public class ArchivedUsersUnarchiveEnablesApiTests : ArchivedUsersApiTestBase
{
    protected override void CustomTestSetup(IUmbracoBuilder builder)
        => builder.Services.PostConfigure<ArchivedUsersSettings>(s => s.UnarchiveEnablesUser = true);

    [Test]
    public async Task Non_admin_unarchive_archived_admin_with_enable_true_returns_NotPermitted_and_admin_stays_disabled()
    {
        IUser archivedAdmin = await ArchivedAsync(await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync()), await Users.CreateAdminAsync("Archived admin"));
        HttpClient manager = await CreateAuthenticatedClientAsync(await Users.CreateUserManagerAsync());

        Dictionary<Guid, string> result = await PostStatusesAsync(manager, "unarchive", new { userIds = new[] { archivedAdmin.Key }, enable = true });

        Assert.That(result[archivedAdmin.Key], Is.EqualTo("NotPermitted"));
        Assert.That(await IsApprovedAsync(archivedAdmin.Key), Is.False);
    }

    [Test]
    public async Task Admin_unarchive_with_enable_on_superuser_record_returns_NotPermitted()
    {
        await InsertRecordAsync(Constants.Security.SuperUserKey);
        HttpClient admin = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());

        Dictionary<Guid, string> result = await PostStatusesAsync(admin, "unarchive", new { userIds = new[] { Constants.Security.SuperUserKey }, enable = true });

        Assert.That(result[Constants.Security.SuperUserKey], Is.EqualTo("NotPermitted"));
        Assert.That(await RecordAsync(Constants.Security.SuperUserKey), Is.Not.Null);
    }

    [Test]
    public async Task Enable_true_with_setting_on_and_WasDisabledBefore_true_stays_disabled_and_audit_ReEnabled_false()
    {
        HttpClient admin = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());
        IUser editor = await ArchivedAsync(admin, await Users.CreateEditorAsync(state: UserState.Disabled));

        await PostStatusesAsync(admin, "unarchive", new { userIds = new[] { editor.Key }, enable = true });

        Assert.That(await IsApprovedAsync(editor.Key), Is.False);
        Assert.That((await AuditAsync(editor.Key)).First().ReEnabled, Is.False);
    }

    [Test]
    public async Task Enable_null_with_setting_on_enables_and_audit_ReEnabled_true()
    {
        HttpClient admin = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());
        IUser editor = await ArchivedAsync(admin, await Users.CreateEditorAsync());

        await PostStatusesAsync(admin, "unarchive", new { userIds = new[] { editor.Key } });

        Assert.That(await IsApprovedAsync(editor.Key), Is.True);
        Assert.That((await AuditAsync(editor.Key)).First().ReEnabled, Is.True);
    }
}
