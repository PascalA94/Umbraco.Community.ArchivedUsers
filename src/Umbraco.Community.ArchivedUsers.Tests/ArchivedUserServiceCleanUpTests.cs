using Microsoft.Extensions.Logging;
using NSubstitute;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Community.ArchivedUsers.Core.Models;

namespace Umbraco.Community.ArchivedUsers.Tests;

public class ArchivedUserServiceCleanUpTests
{
    private readonly ServiceFixture _f = new();

    [Fact]
    public async Task Preview_counts_disabled_non_archived_visible_users_and_samples_ten_by_name()
    {
        IUser admin = _f.AddAdmin("Admin");
        var disabled = Enumerable.Range(1, 12).Select(i => _f.AddUser($"Former {i:00}", UserState.Disabled)).ToList();
        _f.AddRecord(disabled[0]);
        _f.AddRecord(disabled[5]);
        _f.AddUser("Active One");

        CleanUpPreview preview = await _f.Create().PreviewCleanUpAsync(admin.Key);

        Assert.Equal(10, preview.Count);
        Assert.Equal(10, preview.Sample.Count);
        Assert.Equal(preview.Sample.Select(s => s.Name).OrderBy(n => n, StringComparer.Ordinal), preview.Sample.Select(s => s.Name));
        Assert.DoesNotContain(preview.Sample, s => s.Id == disabled[0].Key || s.Id == disabled[5].Key);
    }

    [Fact]
    public async Task Preview_with_none_returns_zero_and_empty_sample()
    {
        IUser admin = _f.AddAdmin();
        _f.AddUser("Active");

        CleanUpPreview preview = await _f.Create().PreviewCleanUpAsync(admin.Key);

        Assert.Equal(0, preview.Count);
        Assert.Empty(preview.Sample);
    }

    [Fact]
    public async Task Preview_sample_never_includes_superuser()
    {
        IUser admin = _f.AddAdmin();
        IUser super = _f.AddSuperUser();
        super.UserState.Returns(UserState.Disabled);
        _f.AddUser("Former", UserState.Disabled);

        CleanUpPreview preview = await _f.Create().PreviewCleanUpAsync(admin.Key);

        Assert.Equal(1, preview.Count);
        Assert.DoesNotContain(preview.Sample, s => s.Id == super.Key);
    }

    [Fact]
    public async Task Preview_excludes_invited_users()
    {
        IUser admin = _f.AddAdmin();
        _f.AddUser("Invited", UserState.Invited, loggedIn: false);

        Assert.Equal(0, (await _f.Create().PreviewCleanUpAsync(admin.Key)).Count);
    }

    [Fact]
    public async Task ArchiveAllDisabled_archives_each_with_source_CleanUp()
    {
        IUser admin = _f.AddAdmin();
        _f.AddUser("A", UserState.Disabled);
        _f.AddUser("B", UserState.Disabled);

        IReadOnlyList<UserArchiveResult> results = await _f.Create().ArchiveAllDisabledAsync(admin.Key, "Clean-up 2026");

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(ArchiveResultStatus.Archived, r.Status));
        Assert.All(_f.Records.Values, r => Assert.Equal(ArchiveSource.CleanUp, r.Source));
        Assert.All(_f.AuditRows, r => Assert.Equal("Clean-up 2026", r.Reason));
    }

    [Fact]
    public async Task ArchiveAllDisabled_with_none_writes_nothing()
    {
        IUser admin = _f.AddAdmin();

        IReadOnlyList<UserArchiveResult> results = await _f.Create().ArchiveAllDisabledAsync(admin.Key, null);

        Assert.Empty(results);
        Assert.Empty(_f.Records);
        Assert.Empty(_f.AuditRows);
    }

    [Fact]
    public async Task ArchiveAllDisabled_logs_warning_above_1000()
    {
        IUser admin = _f.AddAdmin();
        for (var i = 0; i < 1001; i++)
        {
            _f.AddUser($"Former {i:0000}", UserState.Disabled);
        }

        await _f.Create().ArchiveAllDisabledAsync(admin.Key, null);

        Assert.Equal(1001, _f.Records.Count);
        Assert.Equal(1, _f.LogCount(LogLevel.Warning));
    }
}
