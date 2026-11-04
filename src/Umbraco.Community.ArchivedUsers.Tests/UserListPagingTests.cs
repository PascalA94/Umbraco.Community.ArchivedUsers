using NSubstitute;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Community.ArchivedUsers.Web.Filters;

namespace Umbraco.Community.ArchivedUsers.Tests;

/// <summary>Review Focus 4: paging edges after dropping archived users.</summary>
public class UserListPagingTests
{
    private static readonly List<Guid> Users = Enumerable.Range(0, 25).Select(_ => Guid.NewGuid()).ToList();

    // Archived at (1-based) positions 2, 11 and 24.
    private static readonly HashSet<Guid> Drop = [Users[1], Users[10], Users[23]];

    private static readonly List<Guid> Kept = Users.Where(u => !Drop.Contains(u)).ToList();

    [Fact]
    public void Pages_concatenate_to_the_kept_users_in_order_with_the_filtered_total()
    {
        var pages = new[] { 0, 10, 20 }.Select(skip => UserListPaging.Apply(Users, k => k, Drop, skip, 10)).ToList();

        Assert.All(pages, p => Assert.Equal(22, p.Total));
        Assert.Equal(Kept, pages.SelectMany(p => p.Page));
    }

    [Theory]
    [InlineData(22, 10)]
    [InlineData(100, 10)]
    [InlineData(0, 0)]
    public void Skip_at_or_past_total_or_take_zero_gives_an_empty_page_and_the_total(int skip, int take)
    {
        var (page, total) = UserListPaging.Apply(Users, k => k, Drop, skip, take);

        Assert.Empty(page);
        Assert.Equal(22, total);
    }

    [Fact]
    public void A_core_slice_that_is_all_archived_is_filled_by_the_next_users()
    {
        var drop = Users.Take(10).ToHashSet();

        var (page, total) = UserListPaging.Apply(Users, k => k, drop, 0, 10);

        Assert.Equal(Users.Skip(10).Take(10), page);
        Assert.Equal(15, total);
    }

    /// <summary>AU-03: an enabled user is never hidden, even with an archive record.</summary>
    [Fact]
    public void DropSet_keeps_archived_but_enabled_key()
    {
        IUser a = User(UserState.Disabled), b = User(UserState.Active);

        Assert.Equal(new[] { a.Key }, UserListPaging.DropSet([a, b], new HashSet<Guid> { a.Key, b.Key }));
    }

    [Fact]
    public void DropSet_ignores_disabled_user_without_record()
    {
        IUser a = User(UserState.Disabled);

        Assert.Empty(UserListPaging.DropSet([a], new HashSet<Guid>()));
    }

    private static IUser User(UserState state)
    {
        var user = Substitute.For<IUser>();
        user.Key.Returns(Guid.NewGuid());
        user.UserState.Returns(state);
        user.IsApproved.Returns(state != UserState.Disabled);
        return user;
    }
}
