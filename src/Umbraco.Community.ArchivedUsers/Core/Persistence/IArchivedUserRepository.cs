using Umbraco.Community.ArchivedUsers.Core.Models;

namespace Umbraco.Community.ArchivedUsers.Core.Persistence;

/// <summary>
/// The package's own two tables. Every method needs an ambient scope and throws
/// <see cref="InvalidOperationException"/> without one, so callers always own the transaction.
/// </summary>
internal interface IArchivedUserRepository
{
    Task<ArchivedUserRecord?> GetAsync(Guid userKey);

    Task<IReadOnlyList<ArchivedUserRecord>> GetAllAsync();

    Task<HashSet<Guid>> GetKeysAsync();

    /// <summary>Inserts the record; false when a record for that user already exists (one record per user).</summary>
    Task<bool> TryInsertAsync(ArchivedUserRecord record);

    /// <summary>
    /// True only for the call that removed the row. This is the cluster de-duplication rule: only the node whose
    /// delete removed the record writes the matching audit row (AU-14b).
    /// </summary>
    Task<bool> DeleteAsync(Guid userKey);

    Task InsertAuditAsync(ArchiveAuditEntry entry);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<ArchiveAuditEntry>> GetAuditForUserAsync(Guid userKey);
}
