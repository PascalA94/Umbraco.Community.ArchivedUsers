using System.Net;
using System.Text.Json;
using Umbraco.Cms.Core.Models.Membership;

namespace Umbraco.Community.ArchivedUsers.IntegrationTests;

/// <summary>HTTP-level core contract tests (docs/plan.md Task 3 Step 5; docs/spikes.md S4).</summary>
[TestFixture]
public class CoreContractApiTests : ApiTestBase
{
#if UMBRACO_HAS_API_USERS
    /// <summary>P12f, T5 baseline: an enabled API user gets a client-credentials token. Records the call Task 11's T5 test uses.</summary>
    [Test]
    [Category("Recording")]
    public async Task Api_user_obtains_client_credentials_token()
    {
        const string clientId = "umbraco-back-office-archived-users-contract";
        const string secret = "contract-test-secret-0123456789";
        await Users.CreateApiUserAsync("Contract API user", clientId, secret);

        HttpResponseMessage response = await RequestClientCredentialsTokenAsync(clientId, secret);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body.RootElement.GetProperty("access_token").GetString(), Is.Not.Empty);
    }
#endif

    protected Task<HttpResponseMessage> RequestClientCredentialsTokenAsync(string clientId, string secret)
        => CreateAnonymousClient().PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = secret,
        }));
}
