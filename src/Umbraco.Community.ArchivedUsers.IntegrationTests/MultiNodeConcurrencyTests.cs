using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Services;

namespace Umbraco.Community.ArchivedUsers.IntegrationTests;

[TestFixture]
public class MultiNodeConcurrencyTests : ArchiveTableTestBase
{
    /// <summary>
    /// Review BQ-3: two services with separate lock tables stand in for two nodes, so the database's unique index is
    /// the only guard. Exactly one record and one Archived row survive, and nobody gets an error.
    /// </summary>
    [Test]
    [Category("Concurrency")]
    [Category("SqlServerOnly")]
    public async Task Concurrent_archive_without_a_shared_lock_relies_on_the_unique_index()
    {
        // SQLite is a single-writer, single-node database: two deferred writers deadlock, and no site runs SQLite
        // on two nodes. The scenario only exists on SQL Server, where the nightly job runs this test.
        if (!string.Equals(GlobalSetupTeardown.TestConfiguration["Tests:Database:DatabaseType"], "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore("Multi-node concurrency needs SQL Server (nightly.yml).");
        }

        IUser admin = await Users.CreateAdminAsync(), editor = await Users.CreateEditorAsync();

        async Task<UserArchiveResult> ArchiveAsNode()
        {
            using IServiceScope scope = Services.CreateScope();
            var service = ActivatorUtilities.CreateInstance<ArchivedUserService>(scope.ServiceProvider, new ArchiveUserLocks());
            return (await service.ArchiveAsync(admin.Key, new HashSet<Guid> { editor.Key }, "Left", ArchiveSource.Manual)).Single();
        }

        UserArchiveResult[] results = await Task.WhenAll(Task.Run(ArchiveAsNode), Task.Run(ArchiveAsNode));

        Assert.That(results.Count(r => r.Status == ArchiveResultStatus.Archived), Is.EqualTo(1), string.Join(",", results.Select(r => r.Status)));
        Assert.That(results.Count(r => r.Status == ArchiveResultStatus.AlreadyArchived), Is.EqualTo(1));
        Assert.That(await RecordAsync(editor.Key), Is.Not.Null);
        Assert.That((await AuditAsync(editor.Key)).Count(r => r.Action == ArchiveAuditAction.Archived), Is.EqualTo(1));
        Assert.That((await UserService.GetAsync(editor.Key))!.IsApproved, Is.False);
    }
}
