namespace Umbraco.Community.ArchivedUsers.Core.Models;

/// <summary>
/// Audit row actions. Stored as a string. <see cref="Exported"/> and <see cref="AuditPurged"/> are written from v0.2;
/// they are declared now so the column's value set is fixed from the first release.
/// </summary>
public enum ArchiveAuditAction
{
    Archived,
    Unarchived,
    AutoDisabled,
    AutoArchived,
    Exported,
    AuditPurged,
}
