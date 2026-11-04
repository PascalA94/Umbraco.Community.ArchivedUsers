using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Security.OperationStatus;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Strings;

namespace Umbraco.Community.ArchivedUsers.IntegrationTests;

/// <summary>
/// Creates users for integration tests through core services only (docs/plan.md Task 3 Interfaces).
/// Every user gets a unique email, so tests that share a fixture database never collide.
/// </summary>
public sealed class TestUsers
{
    public const string UserManagersAlias = "userManagers";
    public const string Password = "Test-Password-1234567890!";

    private readonly IServiceProvider _services;

    public TestUsers(IServiceProvider services) => _services = services;

    private IUserService UserService => _services.GetRequiredService<IUserService>();

    private IUserGroupService UserGroupService => _services.GetRequiredService<IUserGroupService>();

    public async Task<IUser> CreateAsync(string name, string[] groupAliases, UserState state = UserState.Active, bool hasLoggedIn = true)
    {
        var groupKeys = new HashSet<Guid>();
        foreach (var alias in groupAliases)
        {
            IUserGroup group = await UserGroupService.GetAsync(alias)
                ?? throw new InvalidOperationException($"No user group '{alias}'.");
            groupKeys.Add(group.Key);
        }

        var email = $"{Guid.NewGuid():N}@example.test";
        Attempt<UserCreationResult, UserOperationStatus> created = await UserService.CreateAsync(
            Constants.Security.SuperUserKey,
            new UserCreateModel { Email = email, UserName = email, Name = name, UserGroupKeys = groupKeys },
            approveUser: true);
        if (!created.Success || created.Result.CreatedUser is null)
        {
            throw new InvalidOperationException($"Could not create test user: {created.Status}");
        }

        IUser user = (await UserService.GetAsync(created.Result.CreatedUser.Key))!;
        if (hasLoggedIn)
        {
            user.LastLoginDate = DateTime.UtcNow.AddDays(-10);
        }

        switch (state)
        {
            case UserState.Disabled:
                user.IsApproved = false;
                break;
            case UserState.Invited:
                user.IsApproved = false;
                user.LastLoginDate = null;
                user.InvitedDate = DateTime.UtcNow;
                break;
        }

        UserService.Save(user);
        return (await UserService.GetAsync(user.Key))!;
    }

    /// <summary>Disables and locks out a user; core then reports UserState.LockedOut, not Disabled (review BS-1).</summary>
    public async Task<IUser> DisableAndLockOutAsync(IUser user)
    {
        IUser reloaded = (await UserService.GetAsync(user.Key))!;
        reloaded.IsApproved = false;
        reloaded.IsLockedOut = true;
        reloaded.LastLockoutDate = DateTime.UtcNow;
        UserService.Save(reloaded);
        return (await UserService.GetAsync(user.Key))!;
    }

    public async Task<IUserGroup> EnsureUserManagersGroupAsync()
    {
        IUserGroup? existing = await UserGroupService.GetAsync(UserManagersAlias);
        if (existing is not null)
        {
            return existing;
        }

        var group = new UserGroup(_services.GetRequiredService<IShortStringHelper>())
        {
            Alias = UserManagersAlias,
            Name = "User managers",
            HasAccessToAllLanguages = true,
        };
        group.AddAllowedSection(Constants.Applications.Users);
        group.AddAllowedSection(Constants.Applications.Content);

        Attempt<IUserGroup, UserGroupOperationStatus> created = await UserGroupService.CreateAsync(group, Constants.Security.SuperUserKey);
        return created.Success ? created.Result : throw new InvalidOperationException($"Could not create group: {created.Status}");
    }

    /// <summary>A non-admin with Users section access (user managers group plus editor).</summary>
    public async Task<IUser> CreateUserManagerAsync(string name = "User Manager")
    {
        await EnsureUserManagersGroupAsync();
        return await CreateAsync(name, [UserManagersAlias, "editor"]);
    }

    public Task<IUser> CreateAdminAsync(string name = "Admin") => CreateAsync(name, [Constants.Security.AdminGroupAlias]);

    public Task<IUser> CreateEditorAsync(string name = "Editor", UserState state = UserState.Active, bool hasLoggedIn = true)
        => CreateAsync(name, ["editor"], state, hasLoggedIn);

#if UMBRACO_HAS_API_USERS
    /// <summary>A v18 API user (<see cref="UserKind.Api"/>) with one client credential, in the admin group unless told otherwise.</summary>
    public async Task<IUser> CreateApiUserAsync(string name, string clientId, string secret, string groupAlias = Constants.Security.AdminGroupAlias)
    {
        IUserGroup group = (await UserGroupService.GetAsync(groupAlias))!;
        var email = $"{Guid.NewGuid():N}@example.test";
        Attempt<UserCreationResult, UserOperationStatus> created = await UserService.CreateAsync(
            Constants.Security.SuperUserKey,
            new UserCreateModel { Email = email, UserName = email, Name = name, Kind = UserKind.Api, UserGroupKeys = { group.Key } },
            approveUser: true);
        if (!created.Success || created.Result.CreatedUser is null)
        {
            throw new InvalidOperationException($"Could not create API user: {created.Status}");
        }

        Guid key = created.Result.CreatedUser.Key;
        // The manager prefixes the client id with "umbraco-back-office-" unless it already starts with it.
        using IServiceScope scope = _services.CreateScope();
        Attempt<BackOfficeUserClientCredentialsOperationStatus> saved = await scope.ServiceProvider
            .GetRequiredService<IBackOfficeUserClientCredentialsManager>()
            .SaveAsync(key, clientId, secret);
        if (!saved.Success)
        {
            throw new InvalidOperationException($"Could not add client credentials: {saved.Result}");
        }

        return (await UserService.GetAsync(key))!;
    }
#endif
}
