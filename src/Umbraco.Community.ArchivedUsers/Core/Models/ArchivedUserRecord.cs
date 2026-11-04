namespace Umbraco.Community.ArchivedUsers.Core.Models;

/// <summary>The archive flag for one user. <c>ArchivedByUserKey</c> null means the system archived them.</summary>
public sealed record ArchivedUserRecord(
    Guid UserKey,
    DateTime ArchivedUtc,
    Guid? ArchivedByUserKey,
    ArchiveSource Source,
    string? Reason,
    bool WasDisabledBefore);
