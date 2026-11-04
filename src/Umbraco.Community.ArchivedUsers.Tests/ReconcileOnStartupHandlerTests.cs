using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.ArchivedUsers.Core.Handlers;
using Umbraco.Community.ArchivedUsers.Core.Migrations;
using Umbraco.Community.ArchivedUsers.Core.Services;

namespace Umbraco.Community.ArchivedUsers.Tests;

/// <summary>Task 18 finding: on a fresh install the tables don't exist yet, and core logs the failed query as an Error.</summary>
public class ReconcileOnStartupHandlerTests
{
    private readonly IArchivedUserService _service = Substitute.For<IArchivedUserService>();
    private readonly IKeyValueService _keyValues = Substitute.For<IKeyValueService>();

    private ReconcileOnStartupHandler Create()
    {
        var runtime = Substitute.For<IRuntimeState>();
        runtime.Level.Returns(RuntimeLevel.Run);
        var services = new ServiceCollection().AddSingleton(_service).BuildServiceProvider();
        return new ReconcileOnStartupHandler(runtime, services.GetRequiredService<IServiceScopeFactory>(), new ArchivedUsersSchemaState(_keyValues), NullLogger<ReconcileOnStartupHandler>.Instance);
    }

    private const string StateKey = "Umbraco.Core.Upgrader.State+" + ArchivedUsersMigrationPlan.PlanName;

    [Fact]
    public async Task Does_not_reconcile_before_the_package_tables_exist()
    {
        _keyValues.GetValue(StateKey).Returns((string?)null);

        await Create().HandleAsync(new UmbracoApplicationStartedNotification(false), CancellationToken.None);

        await _service.DidNotReceiveWithAnyArgs().ReconcileAsync(default);
    }

    [Fact]
    public async Task Reconciles_once_the_migration_has_run()
    {
        _keyValues.GetValue(StateKey).Returns(ArchivedUsersMigrationPlan.TablesState);

        await Create().HandleAsync(new UmbracoApplicationStartedNotification(false), CancellationToken.None);

        await _service.Received(1).ReconcileAsync(Arg.Any<CancellationToken>());
    }
}
