using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Persistence;

namespace Umbraco.Community.ArchivedUsers.IntegrationTests;

[TestFixture]
public class ArchivedUserRepositoryTests : ArchivedUsersIntegrationTestBase
{
    private IArchivedUserRepository Repository => GetRequiredService<IArchivedUserRepository>();

    [SetUp]
    public async Task Migrate() => await RunPackageMigrationsAsync();

    private static ArchivedUserRecord Record(Guid? key = null, ArchiveSource source = ArchiveSource.Manual, string? reason = "Left 24 Oct")
        => new(key ?? Guid.NewGuid(), new DateTime(2026, 10, 24, 9, 30, 15, DateTimeKind.Utc), Guid.NewGuid(), source, reason, false);

    [Test]
    public async Task Insert_then_get_round_trips_with_utc_kind()
    {
        ArchivedUserRecord record = Record();
        using (IScope scope = ScopeProvider.CreateScope())
        {
            Assert.That(await Repository.TryInsertAsync(record), Is.True);
            scope.Complete();
        }

        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            ArchivedUserRecord? read = await Repository.GetAsync(record.UserKey);
            Assert.That(read, Is.EqualTo(record));
            Assert.That(read!.ArchivedUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        }
    }

    /// <summary>Review Focus 1: the same user archived twice leaves exactly one record.</summary>
    [Test]
    public async Task TryInsert_duplicate_key_returns_false_and_keeps_one_row()
    {
        ArchivedUserRecord record = Record();
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);

        Assert.That(await Repository.TryInsertAsync(record), Is.True);
        Assert.That(await Repository.TryInsertAsync(record with { Reason = "second" }), Is.False);

        Assert.That((await Repository.GetAllAsync()).Count(r => r.UserKey == record.UserKey), Is.EqualTo(1));
        Assert.That((await Repository.GetAsync(record.UserKey))!.Reason, Is.EqualTo("Left 24 Oct"));
    }

    [Test]
    public async Task Enums_are_stored_as_strings()
    {
        ArchivedUserRecord record = Record(source: ArchiveSource.CleanUp);
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        await Repository.TryInsertAsync(record);

        var raw = scope.Database.ExecuteScalar<string>("SELECT source FROM archivedUsersUser WHERE userKey = @0", record.UserKey);

        Assert.That(raw, Is.EqualTo("CleanUp"));
    }

    [Test]
    public async Task Delete_record_keeps_audit_rows()
    {
        ArchivedUserRecord record = Record();
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        await Repository.TryInsertAsync(record);
        await Repository.InsertAuditAsync(new ArchiveAuditEntry(record.UserKey, ArchiveAuditAction.Archived, Guid.NewGuid(), DateTime.UtcNow, ArchiveSource.Manual, "r", "Jo"));

        Assert.That(await Repository.DeleteAsync(record.UserKey), Is.True);
        Assert.That(await Repository.DeleteAsync(record.UserKey), Is.False, "only the call that removed the row returns true");

        Assert.That(await Repository.GetAsync(record.UserKey), Is.Null);
        Assert.That(await Repository.GetAuditForUserAsync(record.UserKey), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Audit_round_trips_ReEnabled_and_Context()
    {
        var key = Guid.NewGuid();
        var older = new ArchiveAuditEntry(key, ArchiveAuditAction.Archived, null, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), ArchiveSource.Bulk, null, "Jo");
        var newer = new ArchiveAuditEntry(key, ArchiveAuditAction.Unarchived, Guid.NewGuid(), new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), ArchiveSource.Manual, null, "Jo", ReEnabled: true, Context: "run-42");
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        await Repository.InsertAuditAsync(older);
        await Repository.InsertAuditAsync(newer);

        IReadOnlyList<ArchiveAuditEntry> rows = await Repository.GetAuditForUserAsync(key);

        Assert.That(rows, Is.EqualTo(new[] { newer, older }), "newest first, every field equal");
        Assert.That(rows[0].PerformedUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    /// <summary>AU-12: user text never reaches SQL as code.</summary>
    [Test]
    public async Task Search_text_is_parameterised()
    {
        const string hostile = "'; DROP TABLE archivedUsersAudit;--";
        ArchivedUserRecord record = Record(reason: hostile);
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);

        await Repository.TryInsertAsync(record);
        await Repository.InsertAuditAsync(new ArchiveAuditEntry(record.UserKey, ArchiveAuditAction.Archived, null, DateTime.UtcNow, ArchiveSource.Manual, hostile, hostile));

        Assert.That((await Repository.GetAsync(record.UserKey))!.Reason, Is.EqualTo(hostile));
        Assert.That((await Repository.GetAuditForUserAsync(record.UserKey))[0].UserNameSnapshot, Is.EqualTo(hostile));
        Assert.That(Tables(), Does.Contain("archivedUsersAudit").IgnoreCase.And.Contain("archivedUsersUser").IgnoreCase);
    }

    [Test]
    public async Task GetKeys_returns_every_archived_key()
    {
        ArchivedUserRecord a = Record(), b = Record();
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        await Repository.TryInsertAsync(a);
        await Repository.TryInsertAsync(b);

        Assert.That(await Repository.GetKeysAsync(), Is.EquivalentTo(new[] { a.UserKey, b.UserKey }));
    }

    [Test]
    public void Repository_without_scope_throws()
        => Assert.ThrowsAsync<InvalidOperationException>(async () => await Repository.GetAllAsync());
}
