using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.AuthorizationStatus;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Community.ArchivedUsers.Core.Configuration;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Persistence;
using Umbraco.Community.ArchivedUsers.Core.Security;
using Umbraco.Community.ArchivedUsers.Core.Services;

namespace Umbraco.Community.ArchivedUsers.Tests;

/// <summary>
/// Substitutes for every dependency of <see cref="ArchivedUserService"/>, plus helpers to create users.
/// The repository substitute behaves like a small in-memory table so tests read naturally.
/// </summary>
internal sealed class ServiceFixture
{
    public IUserService UserService { get; } = Substitute.For<IUserService>();
    public IUserPermissionService Permissions { get; } = Substitute.For<IUserPermissionService>();
    public ISecurityStampRotator StampRotator { get; } = Substitute.For<ISecurityStampRotator>();
    public IAuditService Audit { get; } = Substitute.For<IAuditService>();
    public IArchivedUserRepository Repository { get; } = Substitute.For<IArchivedUserRepository>();
    public ICoreScopeProvider ScopeProvider { get; } = Substitute.For<ICoreScopeProvider>();
    public ICoreScope Scope { get; } = Substitute.For<ICoreScope>();
    public IEventAggregator Events { get; } = Substitute.For<IEventAggregator>();
    public IEventMessagesFactory MessagesFactory { get; } = Substitute.For<IEventMessagesFactory>();
    public RecordingLocks Locks { get; } = new();
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero));
    public FakeLogger<ArchivedUserService> Logger { get; } = new();
    public ArchivedUsersSettings Settings { get; } = new();

    public Dictionary<Guid, ArchivedUserRecord> Records { get; } = new();
    public List<ArchiveAuditEntry> AuditRows { get; } = new();
    private readonly Dictionary<Guid, IUser> _users = new();
    private int _nextId = 100;

    public ServiceFixture()
    {
        ScopeProvider.CreateCoreScope(default, default, default, default, default, default, default).ReturnsForAnyArgs(Scope);
        MessagesFactory.Get().Returns(_ => new EventMessages());
        UserService.GetAsync(Arg.Any<Guid>()).Returns(ci => _users.GetValueOrDefault(ci.Arg<Guid>()));
        UserService.GetAsync(Arg.Any<IEnumerable<Guid>>()).Returns(ci => ci.Arg<IEnumerable<Guid>>().Where(_users.ContainsKey).Select(k => _users[k]).ToList());
        UserService.DisableAsync(Arg.Any<Guid>(), Arg.Any<ISet<Guid>>()).Returns(ci =>
        {
            foreach (Guid key in ci.Arg<ISet<Guid>>())
            {
                if (_users.TryGetValue(key, out IUser? u))
                {
                    u.UserState.Returns(UserState.Disabled);
                    u.IsApproved.Returns(false);
                }
            }

            return UserOperationStatus.Success;
        });
        UserService.EnableAsync(Arg.Any<Guid>(), Arg.Any<ISet<Guid>>()).Returns(ci =>
        {
            foreach (Guid key in ci.Arg<ISet<Guid>>())
            {
                if (_users.TryGetValue(key, out IUser? u))
                {
                    u.UserState.Returns(UserState.Active);
                    u.IsApproved.Returns(true);
                }
            }

            return UserOperationStatus.Success;
        });
        Permissions.AuthorizeAccessAsync(Arg.Any<IUser>(), Arg.Any<IEnumerable<Guid>>()).Returns(ci =>
        {
            IUser performer = ci.Arg<IUser>();
            bool anyAdmin = ci.Arg<IEnumerable<Guid>>().Any(k => _users.TryGetValue(k, out IUser? t) && UserVisibility.IsAdmin(t));
            return UserVisibility.IsAdmin(performer) || !anyAdmin ? UserAuthorizationStatus.Success : UserAuthorizationStatus.UnauthorizedMissingAdminAccess;
        });
        Audit.AddAsync(default, default, default, default, default, default).ReturnsForAnyArgs(Attempt.Succeed(AuditLogOperationStatus.Success));

        Repository.GetAsync(Arg.Any<Guid>()).Returns(ci => Records.GetValueOrDefault(ci.Arg<Guid>()));
        Repository.GetAllAsync().Returns(_ => Records.Values.ToList());
        Repository.GetKeysAsync().Returns(_ => Records.Keys.ToHashSet());
        Repository.TryInsertAsync(Arg.Any<ArchivedUserRecord>()).Returns(ci => Records.TryAdd(ci.Arg<ArchivedUserRecord>().UserKey, ci.Arg<ArchivedUserRecord>()));
        Repository.DeleteAsync(Arg.Any<Guid>()).Returns(ci => Records.Remove(ci.Arg<Guid>()));
        Repository.InsertAuditAsync(Arg.Do<ArchiveAuditEntry>(AuditRows.Add)).Returns(Task.CompletedTask);
        Repository.GetAuditForUserAsync(Arg.Any<Guid>()).Returns(ci => AuditRows.Where(r => r.UserKey == ci.Arg<Guid>()).Reverse().ToList());
    }

    public ArchivedUserService Create()
        => new(
            UserService,
            Permissions,
            StampRotator,
            Audit,
            Repository,
            ScopeProvider,
            Events,
            MessagesFactory,
            Locks,
            Time,
            Options(Settings),
            Logger);

    public IUser AddUser(string name = "User", UserState state = UserState.Active, bool admin = false, Guid? key = null, DateTime? lastLogin = null, bool loggedIn = true)
    {
        IUser user = UserVisibilityTests.User(key ?? Guid.NewGuid(), admin ? [Constants.Security.AdminGroupAlias] : ["editor"]);
        user.Id.Returns(_nextId++);
        user.Name.Returns(name);
        user.Email.Returns($"{name.Replace(' ', '.').ToLowerInvariant()}@example.test");
        user.UserState.Returns(state);
        user.IsApproved.Returns(state is not (UserState.Disabled or UserState.Invited));
        user.LastLoginDate.Returns(loggedIn ? lastLogin ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) : null);
        _users[user.Key] = user;
        return user;
    }

    public IUser AddAdmin(string name = "Admin") => AddUser(name, admin: true);

    public IUser AddSuperUser() => AddUser("Super", admin: true, key: Constants.Security.SuperUserKey);

    public void RemoveUser(Guid key) => _users.Remove(key);

    public ArchivedUserRecord AddRecord(IUser user, bool wasDisabledBefore = false, Guid? archivedBy = null)
    {
        var record = new ArchivedUserRecord(user.Key, Time.GetUtcNow().UtcDateTime.AddDays(-1), archivedBy, ArchiveSource.Manual, "earlier", wasDisabledBefore);
        Records[user.Key] = record;
        return record;
    }

    public static IOptionsMonitor<T> Options<T>(T value)
    {
        var monitor = Substitute.For<IOptionsMonitor<T>>();
        monitor.CurrentValue.Returns(value);
        return monitor;
    }

    public IEnumerable<string> LogText()
        => Logger.Collector.GetSnapshot().Select(r => r.Message + " " + string.Join(" ", r.StructuredState?.Select(kv => kv.Value) ?? []));

    public int LogCount(LogLevel level) => Logger.Collector.GetSnapshot().Count(r => r.Level == level);
}

/// <summary>Records lock acquire and release order; behaves like a real (uncontended) lock.</summary>
internal sealed class RecordingLocks : IArchiveUserLocks
{
    public List<string> Events { get; } = new();

    private int _held;

    public int MaxHeldAtOnce { get; private set; }

    public Task<IDisposable> AcquireAsync(Guid userKey, CancellationToken cancellationToken = default)
    {
        Events.Add($"acquire:{userKey}");
        _held++;
        MaxHeldAtOnce = Math.Max(MaxHeldAtOnce, _held);
        return Task.FromResult<IDisposable>(new Handle(this, userKey));
    }

    private sealed class Handle(RecordingLocks owner, Guid key) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owner._held--;
            owner.Events.Add($"release:{key}");
        }
    }
}
