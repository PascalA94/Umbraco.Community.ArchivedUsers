using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Community.ArchivedUsers.Core.Security;

namespace Umbraco.Community.ArchivedUsers.Tests;

public class UserVisibilityTests
{
    internal static IUser User(Guid? key = null, params string[] groupAliases)
    {
        var user = Substitute.For<IUser>();
        user.Key.Returns(key ?? Guid.NewGuid());
        var groups = groupAliases.Select(alias =>
        {
            var group = Substitute.For<IReadOnlyUserGroup>();
            group.Alias.Returns(alias);
            return group;
        }).ToArray();
        user.Groups.Returns(groups);
        return user;
    }

    private static readonly Guid SuperKey = Constants.Security.SuperUserKey;

    [Fact]
    public void Superuser_target_hidden_from_everyone_but_superuser()
    {
        IUser super = User(SuperKey, Constants.Security.AdminGroupAlias);
        IUser admin = User(null, Constants.Security.AdminGroupAlias);

        Assert.False(UserVisibility.CanSee(admin, super));
        Assert.True(UserVisibility.CanSee(super, super));
    }

    [Fact]
    public void Admin_target_hidden_from_non_admin()
        => Assert.False(UserVisibility.CanSee(User(null, "userManagers", "editor"), User(null, Constants.Security.AdminGroupAlias)));

    [Fact]
    public void Admin_target_visible_to_admin()
        => Assert.True(UserVisibility.CanSee(User(null, Constants.Security.AdminGroupAlias), User(null, Constants.Security.AdminGroupAlias)));

    [Fact]
    public void Non_admin_target_visible_to_non_admin()
        => Assert.True(UserVisibility.CanSee(User(null, "userManagers"), User(null, "editor")));

    [Fact]
    public void Performer_can_see_self()
    {
        IUser me = User(null, "editor");
        Assert.True(UserVisibility.CanSee(me, me));
    }

    [Fact]
    public void IsAdmin_true_only_for_admin_group_member()
    {
        Assert.True(UserVisibility.IsAdmin(User(null, "editor", Constants.Security.AdminGroupAlias)));
        Assert.False(UserVisibility.IsAdmin(User(null, "editor", "userManagers")));
        Assert.False(UserVisibility.IsAdmin(User(null)));
    }

    [Fact]
    public void IsSuperUser_true_only_for_SuperUserKey()
    {
        Assert.True(UserVisibility.IsSuperUser(SuperKey));
        Assert.False(UserVisibility.IsSuperUser(Guid.NewGuid()));
    }
}
