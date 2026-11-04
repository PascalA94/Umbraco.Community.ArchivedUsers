namespace Umbraco.Community.ArchivedUsers.Core.Models;

/// <summary>How an archive or unarchive happened. Stored as a string; values are permanent once released.</summary>
public enum ArchiveSource
{
    Manual,
    Bulk,
    CleanUp,
    Policy,
    Automate,
    CoreEnable,
    Reconcile,
}
