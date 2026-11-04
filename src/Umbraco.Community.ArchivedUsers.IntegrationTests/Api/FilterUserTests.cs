using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.ArchivedUsers.Core.Configuration;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Services;

namespace Umbraco.Community.ArchivedUsers.IntegrationTests.Api;

/// <summary>Seeds 25 users named "fltr 01" to "fltr 25" and archives some of them through the service.</summary>
public abstract class FilterUserTestBase : ArchivedUsersApiTestBase
{
    protected const string FilterUser = ManagementApi + "filter/user";

    protected IUser Admin { get; private set; } = null!;

    protected HttpClient AdminClient { get; private set; } = null!;

    protected List<IUser> Seeded { get; } = new();

    /// <summary>1-based positions to archive.</summary>
    protected virtual int[] ArchivedPositions => [2, 11, 24];

    protected IEnumerable<IUser> ArchivedUsers => ArchivedPositions.Select(p => Seeded[p - 1]);

    [SetUp]
    public async Task Seed()
    {
        Seeded.Clear();
        Admin = await Users.CreateAdminAsync("Filter Admin");
        AdminClient = await CreateAuthenticatedClientAsync(Admin);
        for (var i = 1; i <= 25; i++)
        {
            Seeded.Add(await Users.CreateEditorAsync($"fltr {i:00}"));
        }

        using IServiceScope scope = Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IArchivedUserService>();
        var results = await service.ArchiveAsync(Admin.Key, ArchivedUsers.Select(u => u.Key).ToHashSet(), null, ArchiveSource.Bulk);
        Assert.That(results.Select(r => r.Status), Is.All.EqualTo(ArchiveResultStatus.Archived));
    }

    protected async Task<(List<Guid> Ids, int Total)> GetAsync(HttpClient client, string query)
    {
        JsonElement json = await ReadJsonAsync(await client.GetAsync(FilterUser + "?" + query));
        return (json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList(), json.GetProperty("total").GetInt32());
    }

    protected List<Guid> ExpectedByName(bool descending = false)
    {
        IEnumerable<IUser> kept = Seeded.Where(u => !ArchivedUsers.Contains(u));
        return (descending ? kept.OrderByDescending(u => u.Name) : kept.OrderBy(u => u.Name)).Select(u => u.Key).ToList();
    }
}

[TestFixture]
public class FilterUserTests : FilterUserTestBase
{
    [Test]
    public async Task Filter_user_excludes_archived_with_correct_total_across_three_pages()
    {
        var all = new List<Guid>();
        foreach (int skip in new[] { 0, 10, 20 })
        {
            (List<Guid> ids, int total) = await GetAsync(AdminClient, $"filter=fltr&orderBy=Name&orderDirection=Ascending&skip={skip}&take=10");
            Assert.That(total, Is.EqualTo(22));
            all.AddRange(ids);
        }

        Assert.That(all, Is.EqualTo(ExpectedByName()));
    }

    [Test]
    public async Task Filter_user_honours_userStates_filter_and_orderBy()
    {
        (List<Guid> ids, int total) = await GetAsync(AdminClient, "filter=fltr&userStates=Active&orderBy=Name&orderDirection=Descending&take=100");

        Assert.That(ids, Is.EqualTo(ExpectedByName(descending: true)));
        Assert.That(total, Is.EqualTo(22));
    }

    /// <summary>P7: the user group member list and the group's "Choose" picker call this with userGroupIds.</summary>
    [Test]
    public async Task Filter_user_with_userGroupIds_hides_archived_disabled_members()
    {
        (List<Guid> ids, int total) = await GetAsync(AdminClient, $"userGroupIds={Constants.Security.EditorGroupKey}&filter=fltr&take=100");

        Assert.That(ids, Is.EquivalentTo(ExpectedByName()));
        Assert.That(total, Is.EqualTo(22));
    }

    /// <summary>P7: "fltr 0" matches fltr 01 to 09; fltr 02 is archived.</summary>
    [Test]
    public async Task Filter_user_with_name_filter_hides_archived_match()
    {
        (List<Guid> ids, int total) = await GetAsync(AdminClient, "filter=fltr%200&take=100");

        Assert.That(total, Is.EqualTo(8));
        Assert.That(ids, Does.Not.Contain(Seeded[1].Key));
    }

    /// <summary>Review BQ-1: no skip or take means core's defaults (skip 0, take 100), as S1 found.</summary>
    [Test]
    public async Task Filter_user_without_skip_or_take_uses_core_defaults()
    {
        (List<Guid> ids, int total) = await GetAsync(AdminClient, "filter=fltr");

        Assert.That(total, Is.EqualTo(22));
        Assert.That(ids, Has.Count.EqualTo(22));
    }

    /// <summary>Review BQ-1, Review Focus 4: paging edges over HTTP.</summary>
    [TestCase(22, 10)]
    [TestCase(100, 10)]
    [TestCase(0, 0)]
    public async Task Filter_user_skip_past_total_or_take_zero_returns_no_items_and_the_total(int skip, int take)
    {
        (List<Guid> ids, int total) = await GetAsync(AdminClient, $"filter=fltr&skip={skip}&take={take}");

        Assert.That(ids, Is.Empty);
        Assert.That(total, Is.EqualTo(22));
    }

    /// <summary>Review BS-1: an archived user who is disabled and locked out is hidden too.</summary>
    [Test]
    public async Task Filter_user_hides_archived_disabled_locked_out_user()
    {
        IUser locked = await Users.DisableAndLockOutAsync(ArchivedUsers.First());

        (List<Guid> ids, int total) = await GetAsync(AdminClient, "filter=fltr&take=100");

        Assert.That(locked.UserState, Is.EqualTo(UserState.LockedOut));
        Assert.That(ids, Does.Not.Contain(locked.Key));
        Assert.That(total, Is.EqualTo(22));
    }

    /// <summary>T3, AU-03: an enabled user with a record is never hidden.</summary>
    [Test]
    public async Task Filter_user_shows_archived_but_enabled_user()
    {
        IUser enabled = await Users.CreateEditorAsync("fltr stale");
        await InsertRecordAsync(enabled.Key);

        (List<Guid> ids, int total) = await GetAsync(AdminClient, "filter=fltr&take=100");

        Assert.That(ids, Does.Contain(enabled.Key));
        Assert.That(total, Is.EqualTo(23));
    }

    /// <summary>T8, AU-10: fails open to core's response.</summary>
    [Test]
    public async Task Filter_user_fails_open_when_table_is_missing()
    {
        using (IServiceScope di = Services.CreateScope())
        using (ICoreScope scope = di.ServiceProvider.GetRequiredService<ICoreScopeProvider>().CreateCoreScope())
        {
            ((Umbraco.Cms.Infrastructure.Scoping.IScope)scope).Database.Execute("DROP TABLE archivedUsersUser");
            scope.Complete();
        }

        HttpResponseMessage response = await AdminClient.GetAsync(FilterUser + "?filter=fltr&take=100");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await ReadJsonAsync(response)).GetProperty("total").GetInt32(), Is.EqualTo(25));
    }

    [Test]
    public async Task Item_user_and_user_by_id_still_return_archived_user()
    {
        IUser archived = ArchivedUsers.First();

        HttpResponseMessage item = await AdminClient.GetAsync(ManagementApi + "item/user?id=" + archived.Key);
        HttpResponseMessage byId = await AdminClient.GetAsync(ManagementApi + "user/" + archived.Key);

        Assert.That((await ReadJsonAsync(item)).EnumerateArray().Select(i => i.GetProperty("id").GetGuid()), Does.Contain(archived.Key));
        Assert.That((await ReadJsonAsync(byId)).GetProperty("id").GetGuid(), Is.EqualTo(archived.Key));
    }

    [Test]
    public async Task Current_user_endpoint_unaffected_for_archived_admin_viewer()
        => Assert.That((await ReadJsonAsync(await AdminClient.GetAsync(ManagementApi + "user/current"))).GetProperty("id").GetGuid(), Is.EqualTo(Admin.Key));

    /// <summary>T15: core's rule survives the package's short-circuit.</summary>
    [Test]
    public async Task Non_admin_filter_still_hides_admins_and_superuser()
    {
        HttpClient manager = await CreateAuthenticatedClientAsync(await Users.CreateUserManagerAsync());

        (List<Guid> ids, _) = await GetAsync(manager, "take=1000");

        Assert.That(ids, Does.Not.Contain(Admin.Key).And.Not.Contain(Constants.Security.SuperUserKey));
        Assert.That(ids, Does.Not.Contain(ArchivedUsers.First().Key));
        Assert.That(ids, Does.Contain(Seeded[0].Key));
    }

#if UMBRACO_HAS_API_USERS
    /// <summary>
    /// T9, AU-10, v17 line: core's Filter action at release-17.8.0 has no userKinds parameter, so a userKinds query
    /// is ignored exactly as core ignores it; archived API users are still hidden.
    /// </summary>
    [Test]
    public async Task Filter_user_ignores_userKinds_like_core()
    {
        IUser apiKept = await Users.CreateApiUserAsync("fltr api kept", "umbraco-back-office-fltr-kept", "secret-kept-0123456789-abcdef");
        IUser apiArchived = await Users.CreateApiUserAsync("fltr api archived", "umbraco-back-office-fltr-arch", "secret-arch-0123456789-abcdef");
        await PostStatusesAsync(AdminClient, "archive", new { userIds = new[] { apiArchived.Key } });

        (List<Guid> ids, int total) = await GetAsync(AdminClient, "userKinds=Api&take=1000");

        Assert.That(ids, Does.Contain(apiKept.Key).And.Not.Contain(apiArchived.Key));
        Assert.That(ids, Does.Contain(Seeded[0].Key), "no kind filtering on 17.x");
        Assert.That(total, Is.EqualTo(ids.Count));
    }

    /// <summary>AU-10: an API user (client credentials) gets core's list by default.</summary>
    [Test]
    public async Task Filter_user_as_api_user_returns_core_list_by_default()
    {
        const string clientId = "umbraco-back-office-fltr-reader";
        const string secret = "reader-secret-0123456789-abcdef";
        await Users.CreateApiUserAsync("Reader API user", clientId, secret);
        HttpResponseMessage tokenResponse = await CreateAnonymousClient().PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials", ["client_id"] = clientId, ["client_secret"] = secret,
        }));
        string token = (await ReadJsonAsync(tokenResponse)).GetProperty("access_token").GetString()!;
        HttpClient api = CreateAnonymousClient();
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        (List<Guid> ids, int total) = await GetAsync(api, "filter=fltr%20&take=100");

        Assert.That(ids, Is.SupersetOf(ArchivedUsers.Select(u => u.Key)));
        Assert.That(total, Is.EqualTo(25));
    }
#endif
}

/// <summary>P7: archived users at page boundaries (positions 9, 10 and 19 with take=10).</summary>
[TestFixture]
public class FilterUserBoundaryTests : FilterUserTestBase
{
    protected override int[] ArchivedPositions => [9, 10, 19];

    [Test]
    public async Task Filter_user_paging_with_archived_users_at_page_boundaries()
    {
        var all = new List<Guid>();
        foreach (int skip in new[] { 0, 10, 20 })
        {
            (List<Guid> ids, int total) = await GetAsync(AdminClient, $"filter=fltr&orderBy=Name&orderDirection=Ascending&skip={skip}&take=10");
            Assert.That(total, Is.EqualTo(22));
            all.AddRange(ids);
        }

        Assert.That(all, Is.EqualTo(ExpectedByName()));
    }
}

/// <summary>Hide switch off: the response is exactly core's.</summary>
[TestFixture]
public class FilterUserSettingOffTests : FilterUserTestBase
{
    protected override void CustomTestSetup(IUmbracoBuilder builder)
        => builder.Services.PostConfigure<ArchivedUsersSettings>(s => s.HideArchivedFromUserList = false);

    [Test]
    public async Task Filter_user_with_setting_off_returns_archived_users_and_core_total()
    {
        (List<Guid> ids, int total) = await GetAsync(AdminClient, "filter=fltr&take=100");

        Assert.That(total, Is.EqualTo(25));
        Assert.That(ids, Is.SupersetOf(ArchivedUsers.Select(u => u.Key)));
    }
}

/// <summary>P7: with core's HideDisabledUsersInBackOffice on, core already hides disabled users; the total is not reduced twice.</summary>
[TestFixture]
public class FilterUserHideDisabledTests : FilterUserTestBase
{
    protected override void CustomTestSetup(IUmbracoBuilder builder)
        => builder.Services.PostConfigure<SecuritySettings>(s => s.HideDisabledUsersInBackOffice = true);

    [Test]
    public async Task Filter_user_with_HideDisabledUsersInBackOffice_on_matches_core()
    {
        (List<Guid> ids, int total) = await GetAsync(AdminClient, "filter=fltr&orderBy=Name&orderDirection=Ascending&take=100");

        Assert.That(total, Is.EqualTo(22));
        Assert.That(ids, Is.EqualTo(ExpectedByName()));
    }
}

/// <summary>Review BQ-1: a first core page that is entirely archived is refilled from the following users.</summary>
[TestFixture]
public class FilterUserArchivedFirstPageTests : FilterUserTestBase
{
    protected override int[] ArchivedPositions => [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    [Test]
    public async Task Filter_user_first_page_all_archived_is_filled_by_the_next_users()
    {
        (List<Guid> ids, int total) = await GetAsync(AdminClient, "filter=fltr&orderBy=Name&orderDirection=Ascending&skip=0&take=10");

        Assert.That(total, Is.EqualTo(15));
        Assert.That(ids, Is.EqualTo(Seeded.Skip(10).Take(10).Select(u => u.Key)));
    }
}
