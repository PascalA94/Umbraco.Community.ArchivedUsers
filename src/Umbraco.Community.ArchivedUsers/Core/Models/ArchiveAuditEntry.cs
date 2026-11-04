namespace Umbraco.Community.ArchivedUsers.Core.Models;

/// <summary>
/// One audit row (F8, AU-07). <paramref name="UserNameSnapshot"/> is the name only, never the email (D7).
/// <paramref name="Context"/> holds machine values (run id, automation id), never user text; max 255.
/// </summary>
public sealed record ArchiveAuditEntry(
    Guid UserKey,
    ArchiveAuditAction Action,
    Guid? PerformedByUserKey,
    DateTime PerformedUtc,
    ArchiveSource Source,
    string? Reason,
    string? UserNameSnapshot,
    bool ReEnabled = false,
    string? Context = null);
