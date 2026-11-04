using Microsoft.Extensions.Logging;
using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Community.ArchivedUsers.Core.Models;

namespace Umbraco.Community.ArchivedUsers.Tests;

public class ArchivedUserServiceQueryTests
{
    private readonly ServiceFixture _f = new();

    private IUser Archived(string name, DateTime archivedUtc, bool admin = false, Guid? archivedBy = null, UserState state = UserState.Disabled)
    {
        IUser user = admin ? _f.AddAdmin(name) : _f.AddUser(name);
        user.UserState.Returns(state);
        _f.Records[user.Key] = new ArchivedUserRecord(user.Key, archivedUtc, archivedBy, ArchiveSource.Manual, null, false);
        return user;
    }

    private static DateTime Day(int d) => new(2026, 10, d, 0, 0, 0, DateTimeKind.Utc);

    private Task<PagedModel<ArchivedUserView>> Get(IUser performer, int skip = 0, int take = 100, string? filter = null, ArchivedUserOrder order = ArchivedUserOrder.ArchivedUtc, Direction direction = Direction.Descending)
        => _f.Create().GetArchivedAsync(performer.Key, skip, take, filter, order, direction);

    [Fact]
    public async Task GetArchived_default_order_is_ArchivedUtc_descending()
    {
        IUser admin = _f.AddAdmin();
        Archived("A", Day(1));
        Archived("B", Day(3));
        Archived("C", Day(2));

        PagedModel<ArchivedUserView> page = await Get(admin);

        Assert.Equal(new[] { "B", "C", "A" }, page.Items.Select(v => v.User.Name));
    }

    [Fact]
    public async Task GetArchived_sorts_by_name_ascending()
    {
        IUser admin = _f.AddAdmin();
        Archived("beta", Day(1));
        Archived("Alpha", Day(2));
        Archived("Gamma", Day(3));

        PagedModel<ArchivedUserView> page = await Get(admin, order: ArchivedUserOrder.Name, direction: Direction.Ascending);

        Assert.Equal(new[] { "Alpha", "beta", "Gamma" }, page.Items.Select(v => v.User.Name));
    }

    [Fact]
    public async Task GetArchived_filter_matches_name_or_email_case_insensitively()
    {
        IUser admin = _f.AddAdmin();
        IUser jo = Archived("Jo Bloggs", Day(1));
        IUser sam = Archived("Sam", Day(2));
        sam.Email.Returns("SAM.WORK@example.test");
        Archived("Other", Day(3));

        Assert.Equal(new[] { jo.Key }, (await Get(admin, filter: "bloggs")).Items.Select(v => v.User.Key));
        Assert.Equal(new[] { sam.Key }, (await Get(admin, filter: "sam.work")).Items.Select(v => v.User.Key));
    }

    [Fact]
    public async Task GetArchived_pages_in_memory_with_total_of_filtered_set()
    {
        IUser admin = _f.AddAdmin();
        for (var i = 1; i <= 25; i++)
        {
            Archived($"User {i:00}", Day(1).AddHours(i));
        }

        PagedModel<ArchivedUserView> page = await Get(admin, skip: 20, take: 10, order: ArchivedUserOrder.Name, direction: Direction.Ascending);

        Assert.Equal(25, page.Total);
        Assert.Equal(new[] { "User 21", "User 22", "User 23", "User 24", "User 25" }, page.Items.Select(v => v.User.Name));
    }

    /// <summary>P14.</summary>
    [Fact]
    public async Task GetArchived_take_zero_returns_no_items_and_the_filtered_total()
    {
        IUser admin = _f.AddAdmin();
        Archived("A", Day(1));
        Archived("B", Day(2));
        Archived("C", Day(3));

        PagedModel<ArchivedUserView> page = await Get(admin, take: 0);

        Assert.Empty(page.Items);
        Assert.Equal(3, page.Total);
    }

    [Fact]
    public async Task GetArchived_hides_admins_from_non_admin()
    {
        IUser manager = _f.AddUser("Manager");
        Archived("Archived admin", Day(1), admin: true);
        IUser editor = Archived("Archived editor", Day(2));

        PagedModel<ArchivedUserView> page = await Get(manager);

        Assert.Equal(new[] { editor.Key }, page.Items.Select(v => v.User.Key));
        Assert.Equal(1, page.Total);
    }

    /// <summary>Review Focus 3.</summary>
    [Fact]
    public async Task GetArchived_skips_record_whose_user_no_longer_exists()
    {
        IUser admin = _f.AddAdmin();
        IUser gone = Archived("Gone", Day(1));
        IUser kept = Archived("Kept", Day(2));
        _f.RemoveUser(gone.Key);

        PagedModel<ArchivedUserView> page = await Get(admin);

        Assert.Equal(new[] { kept.Key }, page.Items.Select(v => v.User.Key));
    }

    /// <summary>Review Focus 3: the view reads the current user, so a rename shows straight away.</summary>
    [Fact]
    public async Task GetArchived_shows_current_name_after_rename()
    {
        IUser admin = _f.AddAdmin();
        IUser user = Archived("Old Name", Day(1));
        user.Name.Returns("New Name");

        Assert.Equal("New Name", (await Get(admin)).Items.Single().User.Name);
    }

    /// <summary>Review Focus 5.</summary>
    [Fact]
    public async Task GetArchived_returns_null_ArchivedByName_when_performer_deleted()
    {
        IUser admin = _f.AddAdmin("Admin");
        IUser archiver = _f.AddAdmin("Archiver");
        Archived("Jo", Day(1), archivedBy: archiver.Key);
        Archived("System archived", Day(2));
        _f.RemoveUser(archiver.Key);

        PagedModel<ArchivedUserView> page = await Get(admin, order: ArchivedUserOrder.Name, direction: Direction.Ascending);

        ArchivedUserView jo = page.Items.Single(v => v.User.Name == "Jo");
        Assert.Equal(archiver.Key, jo.Record.ArchivedByUserKey);
        Assert.Null(jo.ArchivedByName);
    }

    [Fact]
    public async Task GetArchived_returns_ArchivedByName_for_existing_performer()
    {
        IUser admin = _f.AddAdmin("Admin");
        Archived("Jo", Day(1), archivedBy: admin.Key);

        Assert.Equal("Admin", (await Get(admin)).Items.Single().ArchivedByName);
    }

    [Fact]
    public async Task GetArchived_logs_warning_above_5000_records()
    {
        IUser admin = _f.AddAdmin();
        for (var i = 0; i < 5001; i++)
        {
            _f.Records[Guid.NewGuid()] = new ArchivedUserRecord(Guid.NewGuid(), Day(1), null, ArchiveSource.Manual, null, false);
        }

        await Get(admin);

        Assert.Equal(1, _f.LogCount(LogLevel.Warning));
    }

    [Fact]
    public async Task GetArchivedUser_returns_null_when_not_visible()
    {
        IUser manager = _f.AddUser("Manager");
        IUser admin = Archived("Archived admin", Day(1), admin: true);
        IUser editor = Archived("Archived editor", Day(1));

        Assert.Null(await _f.Create().GetArchivedUserAsync(manager.Key, admin.Key));
        Assert.NotNull(await _f.Create().GetArchivedUserAsync(manager.Key, editor.Key));
        Assert.Null(await _f.Create().GetArchivedUserAsync(manager.Key, Guid.NewGuid()));
    }

    /// <summary>AU-03, T3, AU-14: a stale record is flagged on read and nothing is written.</summary>
    [Fact]
    public async Task GetArchived_flags_record_of_enabled_user_as_stale_and_writes_nothing()
    {
        IUser admin = _f.AddAdmin();
        Archived("Enabled again", Day(1), state: UserState.Active);

        PagedModel<ArchivedUserView> page = await Get(admin);

        Assert.True(page.Items.Single().IsStale);
        await _f.Repository.DidNotReceiveWithAnyArgs().DeleteAsync(default);
        await _f.Repository.DidNotReceiveWithAnyArgs().InsertAuditAsync(default!);
    }

    [Fact]
    public async Task GetArchivedKeys_and_IsArchived_read_the_table()
    {
        IUser a = Archived("A", Day(1));
        var service = _f.Create();

        Assert.Equal(new[] { a.Key }, await service.GetArchivedKeysAsync());
        Assert.True(await service.IsArchivedAsync(a.Key));
        Assert.False(await service.IsArchivedAsync(Guid.NewGuid()));
    }
}
