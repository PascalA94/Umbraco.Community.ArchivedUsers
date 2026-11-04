namespace Umbraco.Community.ArchivedUsers.Core.Models;

/// <summary>Per-user outcome of an archive or unarchive. Names are part of the API contract.</summary>
public enum ArchiveResultStatus
{
    Archived,
    Unarchived,
    Self,
    Invited,
    AlreadyArchived,
    NotArchived,

    /// <summary>
    /// The performer may not manage this user. Also returned to non-admins for unknown keys, so a key's
    /// status never reveals whether it belongs to an admin (AU-16).
    /// </summary>
    NotPermitted,

    /// <summary>Only ever returned to admins (AU-16).</summary>
    NotFound,
    Cancelled,
    CoreRefused,
}
