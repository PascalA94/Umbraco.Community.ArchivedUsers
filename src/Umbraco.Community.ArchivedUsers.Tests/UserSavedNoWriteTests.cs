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
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Persistence;

namespace Umbraco.Community.ArchivedUsers.Tests;

/// <summary>
/// Every save of an enabled user reaches this handler. On SQLite even an empty DELETE takes the single write lock,
/// so the handler must only write when an archive record exists (alpha.2: no extra write on ordinary saves).
/// </summary>
public class UserSavedNoWriteTests
{
    private const string StateKey = "Umbraco.Core.Upgrader.State+" + ArchivedUsersMigrationPlan.PlanName;
    private readonly IKeyValueService _keyValues = Substitute.For<IKeyValueService>();
    private readonly IArchivedUserRepository _repository = Substitute.For<IArchivedUserRepository>();

    public UserSavedNoWriteTests() => _keyValues.GetValue(StateKey).Returns(ArchivedUsersMigrationPlan.TablesState);

    private static IUser EnabledUser()
    {
        IUser user = Substitute.For<IUser>();
        user.Key.Returns(Guid.NewGuid());
        user.IsApproved.Returns(true);
        user.UserState.Returns(UserState.Active);
        return user;
    }

    private UserSavedArchiveSyncHandler Handler() => new(
        _repository, Substitute.For<ICoreScopeProvider>(), Substitute.For<IBackOfficeSecurityAccessor>(), Substitute.For<IEventAggregator>(),
        Substitute.For<IEventMessagesFactory>(), new FakeTimeProvider(), new ArchivedUsersSchemaState(_keyValues), NullLogger<UserSavedArchiveSyncHandler>.Instance);

    [Fact]
    public async Task Saving_a_user_who_was_never_archived_writes_nothing()
    {
        IUser user = EnabledUser();
        Guid key = user.Key;
        _repository.GetAsync(key).Returns((ArchivedUserRecord?)null);

        await Handler().HandleAsync(new UserSavedNotification(user, new EventMessages()), CancellationToken.None);

        await _repository.DidNotReceiveWithAnyArgs().DeleteAsync(default);
        await _repository.DidNotReceiveWithAnyArgs().InsertAuditAsync(default!);
    }

    [Fact]
    public async Task Enabling_an_archived_user_still_clears_the_record_and_audits()
    {
        IUser user = EnabledUser();
        Guid key = user.Key; // read before Returns: reading a substitute's property inside Returns confuses NSubstitute
        var record = new ArchivedUserRecord(key, DateTime.UtcNow, null, ArchiveSource.Manual, null, false);
        _repository.GetAsync(key).Returns(record);
        _repository.DeleteAsync(key).Returns(true);

        await Handler().HandleAsync(new UserSavedNotification(user, new EventMessages()), CancellationToken.None);

        await _repository.Received(1).DeleteAsync(key);
        await _repository.Received(1).InsertAuditAsync(Arg.Is<ArchiveAuditEntry>(e => e.UserKey == key && e.Action == ArchiveAuditAction.Unarchived));
    }
}
