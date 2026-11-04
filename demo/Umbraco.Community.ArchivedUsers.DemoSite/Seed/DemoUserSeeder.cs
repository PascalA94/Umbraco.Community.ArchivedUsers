using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Strings;

namespace Umbraco.Community.ArchivedUsers.DemoSite.Seed;

/// <summary>
/// Seeds demo users for spikes, end-to-end tests and the backoffice exercise (docs/plan.md, Task 2).
/// Idempotent: does nothing once the last seeded user exists, and fills in any users a failed run left out. Uses only core services.
/// Registered only when <c>ArchivedUsersDemo:Seed</c> is true. Never part of the package.
/// </summary>
public sealed class DemoUserSeeder : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public const string UserManagersAlias = "userManagers";
    private const string EditorAlias = "editor";

    private readonly IUserService _userService;
    private readonly IUserGroupService _userGroupService;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IRuntimeState _runtimeState;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DemoUserSeeder> _logger;

    public DemoUserSeeder(
        IUserService userService,
        IUserGroupService userGroupService,
        IShortStringHelper shortStringHelper,
        IRuntimeState runtimeState,
        IConfiguration configuration,
        ILogger<DemoUserSeeder> logger)
    {
        _userService = userService;
        _userGroupService = userGroupService;
        _shortStringHelper = shortStringHelper;
        _runtimeState = runtimeState;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            _logger.LogInformation("Demo seed skipped: runtime level is {Level}", _runtimeState.Level);
            return;
        }

        if (_userService.GetByEmail("ivy.invited@example.test") is not null)
        {
            _logger.LogInformation("Demo seed skipped: demo users already exist");
            return;
        }

        _logger.LogInformation("Demo seed starting");

        var password = _configuration["ArchivedUsersDemo:UserPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Demo seed skipped: set the user-secret ArchivedUsersDemo:UserPassword");
            return;
        }

        Guid performer = Constants.Security.SuperUserKey;

        IUserGroup managers = await EnsureUserManagersGroupAsync(performer);
        IUserGroup editor = await _userGroupService.GetAsync(EditorAlias)
            ?? throw new InvalidOperationException("The core editor group is missing.");
        IUserGroup admin = await _userGroupService.GetAsync(Constants.Security.AdminGroupAlias)
            ?? throw new InvalidOperationException("The core admin group is missing.");

        await CreateAsync(performer, "Morgan Manager", "morgan@example.test", [managers.Key, editor.Key], password, loggedIn: true);
        await CreateAsync(performer, "Alex Admin", "alex@example.test", [admin.Key], password, loggedIn: true);

        for (var i = 1; i <= 30; i++)
        {
            await CreateAsync(performer, $"Demo User {i:00}", $"demo.user{i:00}@example.test", [editor.Key], password, loggedIn: true);
        }

        // Disabled with a login history: the users core cannot delete, the reason this package exists.
        for (var i = 1; i <= 8; i++)
        {
            IUser user = await CreateAsync(performer, $"Former Editor {i:00}", $"former.editor{i:00}@example.test", [editor.Key], password, loggedIn: true);
            await _userService.DisableAsync(performer, new HashSet<Guid> { user.Key });
        }

        // Disabled and never logged in: the D6 hint case (core Delete works for these).
        for (var i = 1; i <= 2; i++)
        {
            IUser user = await CreateAsync(performer, $"Never Logged In {i:00}", $"never.loggedin{i:00}@example.test", [editor.Key], password, loggedIn: false);
            await _userService.DisableAsync(performer, new HashSet<Guid> { user.Key });
        }

        // An invited user (not approved, invite date set, never logged in) without sending an email.
        IUser invited = await CreateAsync(performer, "Ivy Invited", "ivy.invited@example.test", [editor.Key], password, loggedIn: false);
        invited.IsApproved = false;
        invited.InvitedDate = DateTime.UtcNow;
        _userService.Save(invited);

        _logger.LogInformation("Demo users seeded");
    }

    private async Task<IUserGroup> EnsureUserManagersGroupAsync(Guid performer)
    {
        IUserGroup? existing = await _userGroupService.GetAsync(UserManagersAlias);
        if (existing is not null)
        {
            return existing;
        }

        var group = new UserGroup(_shortStringHelper)
        {
            Alias = UserManagersAlias,
            Name = "User managers",
        };
        group.AddAllowedSection(Constants.Applications.Users);
        group.AddAllowedSection(Constants.Applications.Content);

        Attempt<IUserGroup, UserGroupOperationStatus> created = await _userGroupService.CreateAsync(group, performer);
        return created.Success
            ? created.Result
            : throw new InvalidOperationException($"Could not create the user managers group: {created.Status}");
    }

    private async Task<IUser> CreateAsync(Guid performer, string name, string email, Guid[] groups, string password, bool loggedIn)
    {
        if (_userService.GetByEmail(email) is { } existingUser)
        {
            return existingUser;
        }

        var model = new UserCreateModel
        {
            Email = email,
            UserName = email,
            Name = name,
            UserGroupKeys = groups.ToHashSet(),
        };

        Attempt<UserCreationResult, UserOperationStatus> created = await _userService.CreateAsync(performer, model, approveUser: true);
        IUser user = created.Success && created.Result.CreatedUser is not null
            ? created.Result.CreatedUser
            : throw new InvalidOperationException($"Could not create demo user: {created.Status}");

        Attempt<PasswordChangedModel, UserOperationStatus> changed =
            await _userService.ChangePasswordAsync(performer, new ChangeUserPasswordModel { UserKey = user.Key, NewPassword = password });
        if (!changed.Success)
        {
            _logger.LogWarning("Demo seed: password not set for {UserKey}: {Status}", user.Key, changed.Status);
        }

        if (loggedIn)
        {
            // A login history matters: core refuses to delete users who have logged in.
            IUser reloaded = await _userService.GetAsync(user.Key) ?? user;
            reloaded.LastLoginDate = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 400));
            _userService.Save(reloaded);
            return reloaded;
        }

        return await _userService.GetAsync(user.Key) ?? user;
    }
}
