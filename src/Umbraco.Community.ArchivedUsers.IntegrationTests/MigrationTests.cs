using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.ArchivedUsers.Core.Migrations;

namespace Umbraco.Community.ArchivedUsers.IntegrationTests;

[TestFixture]
public class MigrationTests : ArchivedUsersIntegrationTestBase
{
    private static readonly string[] UserColumns = ["id", "userKey", "archivedUtc", "archivedByUserKey", "source", "reason", "wasDisabledBefore"];
    private static readonly string[] AuditColumns = ["id", "userKey", "action", "performedByUserKey", "performedUtc", "source", "reason", "userNameSnapshot", "reEnabled", "context"];

    [Test]
    public async Task Plan_creates_both_tables()
    {
        await RunPackageMigrationsAsync();

        Assert.That(Tables(), Does.Contain("archivedUsersUser").IgnoreCase.And.Contain("archivedUsersAudit").IgnoreCase);

        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        ISqlSyntaxProvider syntax = scope.Database.SqlContext.SqlSyntax;
        var columns = syntax.GetColumnsInSchema(scope.Database).ToList();
        string[] Cols(string table) => columns.Where(c => c.TableName.Equals(table, StringComparison.OrdinalIgnoreCase)).Select(c => c.ColumnName).ToArray();
        Assert.That(Cols("archivedUsersUser"), Is.EquivalentTo(UserColumns).Using<string>((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase)));
        Assert.That(Cols("archivedUsersAudit"), Is.EquivalentTo(AuditColumns).Using<string>((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase)));

        // Behaviour, not schema introspection: ColumnInfo.IsNullable is not reported reliably by every provider.
        // reEnabled is NOT NULL with default 0; context accepts NULL (AU-07).
        var key = Guid.NewGuid();
        scope.Database.Execute(
            "INSERT INTO archivedUsersAudit (userKey, action, performedUtc, source, context) VALUES (@0, 'Archived', @1, 'Manual', NULL)",
            key,
            DateTime.UtcNow);
        Assert.That(scope.Database.ExecuteScalar<bool>("SELECT reEnabled FROM archivedUsersAudit WHERE userKey = @0", key), Is.False);
        Assert.That(
            () => scope.Database.Execute(
                "INSERT INTO archivedUsersAudit (userKey, action, performedUtc, source, reEnabled) VALUES (@0, 'Archived', @1, 'Manual', NULL)",
                Guid.NewGuid(),
                DateTime.UtcNow),
            Throws.Exception,
            "reEnabled must be NOT NULL");

        var indexes = syntax.GetDefinedIndexes(scope.Database).ToList();
        Assert.That(indexes.Any(i => i.Item1.Equals("archivedUsersUser", StringComparison.OrdinalIgnoreCase) && i.Item3.Equals("userKey", StringComparison.OrdinalIgnoreCase) && i.Item4), Is.True, "unique index on archivedUsersUser.userKey");
        Assert.That(indexes.Any(i => i.Item1.Equals("archivedUsersAudit", StringComparison.OrdinalIgnoreCase) && i.Item3.Equals("userKey", StringComparison.OrdinalIgnoreCase)), Is.True, "index on archivedUsersAudit.userKey");
        Assert.That(indexes.Any(i => i.Item1.Equals("archivedUsersAudit", StringComparison.OrdinalIgnoreCase) && i.Item3.Equals("performedUtc", StringComparison.OrdinalIgnoreCase)), Is.True, "index on archivedUsersAudit.performedUtc");
    }

    [Test]
    public async Task Plan_is_safe_to_rerun()
    {
        await RunPackageMigrationsAsync();
        var before = Tables().Count;

        ExecutedMigrationPlan again = await ExecutePlanFromStartAsync();

        Assert.That(again.Successful, Is.True, again.Exception?.ToString());
        Assert.That(Tables().Count, Is.EqualTo(before));
    }

    /// <summary>AU-14b: another node may create a table between our existence check and our create.</summary>
    [Test]
    public async Task Step_tolerates_table_created_concurrently()
    {
        using (IScope scope = ScopeProvider.CreateScope())
        {
            scope.Database.Execute("CREATE TABLE archivedUsersUser (id INTEGER NOT NULL PRIMARY KEY)");
            scope.Complete();
        }

        ExecutedMigrationPlan result = await ExecutePlanFromStartAsync();

        Assert.That(result.Successful, Is.True, result.Exception?.ToString());
        Assert.That(Tables(), Does.Contain("archivedUsersAudit").IgnoreCase);
    }

    [Test]
    public void Plan_name_and_state_are_permanent()
    {
        var plan = new ArchivedUsersMigrationPlan();

        Assert.That(plan.PackageName, Is.EqualTo("Umbraco.Community.ArchivedUsers"));
        Assert.That(plan.Name, Is.EqualTo("Umbraco.Community.ArchivedUsers"));
        Assert.That(plan.FinalState, Is.EqualTo("archived-users-1"));
    }
}
