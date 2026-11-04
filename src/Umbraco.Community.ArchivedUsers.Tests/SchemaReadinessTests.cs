using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.ArchivedUsers.Core.Handlers;
using Umbraco.Community.ArchivedUsers.Core.Migrations;
using Umbraco.Community.ArchivedUsers.Core.Persistence;

namespace Umbraco.Community.ArchivedUsers.Tests;

/// <summary>Task 18 finding: core saves users during a fresh install, before the package tables exist.</summary>
public class SchemaReadinessTests
{
    private const string StateKey = "Umbraco.Core.Upgrader.State+" + ArchivedUsersMigrationPlan.PlanName;
    private readonly IKeyValueService _keyValues = Substitute.For<IKeyValueService>();
    private readonly IArchivedUserRepository _repository = Substitute.For<IArchivedUserRepository>();

    private static IUser EnabledUser()
    {
        IUser user = Substitute.For<IUser>();
        user.Key.Returns(Guid.NewGuid());
        user.IsApproved.Returns(true);
        user.UserState.Returns(UserState.Active);
        return user;
    }

    private UserSavedArchiveSyncHandler SavedHandler() => new(
        _repository, Substitute.For<ICoreScopeProvider>(), Substitute.For<IBackOfficeSecurityAccessor>(), Substitute.For<IEventAggregator>(),
        Substitute.For<IEventMessagesFactory>(), new FakeTimeProvider(), new ArchivedUsersSchemaState(_keyValues), NullLogger<UserSavedArchiveSyncHandler>.Instance);

    [Fact]
    public async Task Saved_handler_does_nothing_before_the_tables_exist()
    {
        _keyValues.GetValue(StateKey).Returns((string?)null);

        await SavedHandler().HandleAsync(new UserSavedNotification(EnabledUser(), new EventMessages()), CancellationToken.None);

        await _repository.DidNotReceiveWithAnyArgs().DeleteAsync(default);
    }

    [Fact]
    public async Task Deleted_handler_does_nothing_before_the_tables_exist()
    {
        _keyValues.GetValue(StateKey).Returns((string?)null);
        var handler = new UserDeletedArchiveCleanupHandler(_repository, Substitute.For<ICoreScopeProvider>(), new ArchivedUsersSchemaState(_keyValues), NullLogger<UserDeletedArchiveCleanupHandler>.Instance);

        await handler.HandleAsync(new UserDeletedNotification(EnabledUser(), new EventMessages()), CancellationToken.None);

        await _repository.DidNotReceiveWithAnyArgs().DeleteAsync(default);
    }

    [Fact]
    public async Task Saved_handler_acts_once_the_migration_has_run()
    {
        _keyValues.GetValue(StateKey).Returns(ArchivedUsersMigrationPlan.TablesState);

        await SavedHandler().HandleAsync(new UserSavedNotification(EnabledUser(), new EventMessages()), CancellationToken.None);

        await _repository.ReceivedWithAnyArgs(1).DeleteAsync(default);
    }

    [Fact]
    public void Readiness_is_cached_once_true()
    {
        _keyValues.GetValue(StateKey).Returns(ArchivedUsersMigrationPlan.TablesState);
        var state = new ArchivedUsersSchemaState(_keyValues);

        Assert.True(state.IsReady());
        Assert.True(state.IsReady());
        _keyValues.Received(1).GetValue(StateKey);
    }
}
