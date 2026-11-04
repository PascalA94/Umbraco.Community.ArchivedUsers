using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Infrastructure.Install;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;
using Umbraco.Community.ArchivedUsers.Core;
using Umbraco.Community.ArchivedUsers.Core.Migrations;

namespace Umbraco.Community.ArchivedUsers.IntegrationTests;

/// <summary>
/// Service-level tests with the package's core services registered.
/// appsettings.Tests.json turns off unattended package migrations: boot would otherwise run the package plan in
/// the background while a test writes, and two SQLite writers deadlock. Tests run the plan when they need it.
/// </summary>
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
public abstract class ArchivedUsersIntegrationTestBase : UmbracoIntegrationTest
{
    protected TestUsers Users => new(Services);

    protected override void CustomTestSetup(IUmbracoBuilder builder) => builder.AddArchivedUsersCore();

    /// <summary>Runs the package plan the way core's package migration runner does (an Upgrader over the plan).</summary>
    protected async Task RunPackageMigrationsAsync()
    {
        ExecutedMigrationPlan result = await new Upgrader(new ArchivedUsersMigrationPlan()).ExecuteAsync(
            GetRequiredService<IMigrationPlanExecutor>(),
            GetRequiredService<Umbraco.Cms.Core.Scoping.ICoreScopeProvider>(),
            GetRequiredService<Umbraco.Cms.Core.Services.IKeyValueService>());
        Assert.That(result.Successful, Is.True, result.Exception?.ToString());
    }

    /// <summary>Executes the plan from the empty state, which re-runs every step whatever the stored state says.</summary>
    protected async Task<ExecutedMigrationPlan> ExecutePlanFromStartAsync()
    {
        using IScope scope = (IScope)GetRequiredService<Umbraco.Cms.Core.Scoping.ICoreScopeProvider>().CreateCoreScope();
        ExecutedMigrationPlan result = await GetRequiredService<IMigrationPlanExecutor>().ExecutePlanAsync(new ArchivedUsersMigrationPlan(), string.Empty);
        scope.Complete();
        return result;
    }

    protected IReadOnlyList<string> Tables()
    {
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        return scope.Database.SqlContext.SqlSyntax.GetTablesInSchema(scope.Database).ToList();
    }
}
