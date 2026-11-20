#if UMBRACO_19
using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models.Membership;

namespace Umbraco.Community.ArchivedUsers.IntegrationTests.Api;

/// <summary>
/// Security F3. On 19 the cookie is the only session, and core's per-request approval check covers some policies only,
/// so the package's own security stamp rotation (AU-02) is what ends an archived admin's live session.
/// </summary>
[TestFixture]
public class ArchivedAdminSessionTests : ArchivedUsersApiTestBase
{
    // Check the stamp on every request so the test does not depend on the host's concurrent-logins setting.
    protected override void CustomTestSetup(IUmbracoBuilder builder)
        => builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);

    [Test]
    public async Task Archived_admin_existing_cookie_session_gets_401_on_admin_endpoints()
    {
        IUser victim = await Users.CreateAdminAsync("Session admin");
        HttpClient victimClient = await CreateAuthenticatedClientAsync(victim);
        HttpClient actor = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync("Archiving admin"));
        string[] adminPaths = { Root + "/config", ManagementApi + "user/current", ManagementApi + "user?take=1" };

        foreach (string path in adminPaths)
        {
            Assert.That((await victimClient.GetAsync(path)).StatusCode, Is.EqualTo(HttpStatusCode.OK), "before archive " + path);
        }

        await ArchivedAsync(actor, victim);

        foreach (string path in adminPaths)
        {
            Assert.That((await victimClient.GetAsync(path)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "after archive " + path);
        }
    }

    [Test]
    public async Task Predisabled_admin_existing_cookie_session_gets_401_once_archived()
    {
        IUser victim = await Users.CreateAdminAsync("Predisabled admin");
        HttpClient victimClient = await CreateAuthenticatedClientAsync(victim);
        IUser actorUser = await Users.CreateAdminAsync("Archiving admin two");
        HttpClient actor = await CreateAuthenticatedClientAsync(actorUser);

        // Disabled through core first: core's disable does not rotate the stamp, so the old cookie still authenticates and
        // only core's approval check refuses it (403). Pinned exactly: if core ever rotates on disable this becomes 401, and the
        // test would no longer prove the package's own rotation.
        await UserService.DisableAsync(actorUser.Key, new HashSet<Guid> { victim.Key });
        Assert.That((await victimClient.GetAsync(Root + "/config")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        await ArchivedAsync(actor, victim);

        Assert.That((await victimClient.GetAsync(Root + "/config")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await victimClient.GetAsync(ManagementApi + "user/current")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
#endif
