using NPoco;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Extensions;

namespace Umbraco.Community.ArchivedUsers.Core.Persistence;

/// <summary>
/// NPoco repository over the package tables. Every query is built with <c>Sql&lt;ISqlContext&gt;</c> and
/// parameters, never string concatenation (AU-12). Enums are stored as strings and times as UTC.
/// </summary>
internal sealed class ArchivedUserRepository : IArchivedUserRepository
{
    private readonly IScopeAccessor _scopeAccessor;

    public ArchivedUserRepository(IScopeAccessor scopeAccessor) => _scopeAccessor = scopeAccessor;

    private IUmbracoDatabase Database => _scopeAccessor.AmbientScope?.Database
        ?? throw new InvalidOperationException("The archived users repository needs an ambient scope.");

    public async Task<ArchivedUserRecord?> GetAsync(Guid userKey)
    {
        IUmbracoDatabase db = Database;
        Sql<ISqlContext> sql = db.SqlContext.Sql()
            .Select<ArchivedUserDto>()
            .From<ArchivedUserDto>()
            .Where<ArchivedUserDto>(x => x.UserKey == userKey);
        ArchivedUserDto? dto = await db.FirstOrDefaultAsync<ArchivedUserDto>(sql);
        return dto is null ? null : ToRecord(dto);
    }

    public async Task<IReadOnlyList<ArchivedUserRecord>> GetAllAsync()
    {
        IUmbracoDatabase db = Database;
        Sql<ISqlContext> sql = db.SqlContext.Sql().Select<ArchivedUserDto>().From<ArchivedUserDto>();
        List<ArchivedUserDto> dtos = await db.FetchAsync<ArchivedUserDto>(sql);
        return dtos.Select(ToRecord).ToList();
    }

    public async Task<HashSet<Guid>> GetKeysAsync()
    {
        IUmbracoDatabase db = Database;
        Sql<ISqlContext> sql = db.SqlContext.Sql().Select<ArchivedUserDto>(x => x.UserKey).From<ArchivedUserDto>();
        List<Guid> keys = await db.FetchAsync<Guid>(sql);
        return keys.ToHashSet();
    }

    public async Task<bool> TryInsertAsync(ArchivedUserRecord record)
    {
        IUmbracoDatabase db = Database;
        try
        {
            await db.InsertAsync(ToDto(record));
            return true;
        }
        catch (Exception)
        {
            // The unique index on userKey refused a second record. Detecting it by reading back keeps this
            // database-agnostic (SQLite and SQL Server raise different exception types). Anything else rethrows.
            if (await GetAsync(record.UserKey) is not null)
            {
                return false;
            }

            throw;
        }
    }

    public async Task<bool> DeleteAsync(Guid userKey)
    {
        IUmbracoDatabase db = Database;
        Sql<ISqlContext> sql = db.SqlContext.Sql()
            .Delete<ArchivedUserDto>()
            .Where<ArchivedUserDto>(x => x.UserKey == userKey);
        return await db.ExecuteAsync(sql) > 0;
    }

    public async Task InsertAuditAsync(ArchiveAuditEntry entry)
        => await Database.InsertAsync(ToDto(entry));

    public async Task<IReadOnlyList<ArchiveAuditEntry>> GetAuditForUserAsync(Guid userKey)
    {
        IUmbracoDatabase db = Database;
        Sql<ISqlContext> sql = db.SqlContext.Sql()
            .Select<ArchiveAuditDto>()
            .From<ArchiveAuditDto>()
            .Where<ArchiveAuditDto>(x => x.UserKey == userKey)
            .OrderByDescending<ArchiveAuditDto>(x => x.PerformedUtc)
            .OrderByDescending<ArchiveAuditDto>(x => x.Id);
        List<ArchiveAuditDto> dtos = await db.FetchAsync<ArchiveAuditDto>(sql);
        return dtos.Select(ToEntry).ToList();
    }

    // SQLite does not keep DateTimeKind, so every read restores UTC explicitly.
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static ArchivedUserRecord ToRecord(ArchivedUserDto dto) => new(
        dto.UserKey,
        Utc(dto.ArchivedUtc),
        dto.ArchivedByUserKey,
        Enum.Parse<ArchiveSource>(dto.Source),
        dto.Reason,
        dto.WasDisabledBefore);

    private static ArchivedUserDto ToDto(ArchivedUserRecord record) => new()
    {
        UserKey = record.UserKey,
        ArchivedUtc = record.ArchivedUtc.ToUniversalTime(),
        ArchivedByUserKey = record.ArchivedByUserKey,
        Source = record.Source.ToString(),
        Reason = record.Reason,
        WasDisabledBefore = record.WasDisabledBefore,
    };

    private static ArchiveAuditEntry ToEntry(ArchiveAuditDto dto) => new(
        dto.UserKey,
        Enum.Parse<ArchiveAuditAction>(dto.Action),
        dto.PerformedByUserKey,
        Utc(dto.PerformedUtc),
        Enum.Parse<ArchiveSource>(dto.Source),
        dto.Reason,
        dto.UserNameSnapshot,
        dto.ReEnabled,
        dto.Context);

    private static ArchiveAuditDto ToDto(ArchiveAuditEntry entry) => new()
    {
        UserKey = entry.UserKey,
        Action = entry.Action.ToString(),
        PerformedByUserKey = entry.PerformedByUserKey,
        PerformedUtc = entry.PerformedUtc.ToUniversalTime(),
        Source = entry.Source.ToString(),
        Reason = entry.Reason,
        UserNameSnapshot = entry.UserNameSnapshot,
        ReEnabled = entry.ReEnabled,
        Context = entry.Context,
    };
}
