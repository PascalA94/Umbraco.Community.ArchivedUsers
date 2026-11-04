namespace Umbraco.Community.ArchivedUsers.Core.Models;

/// <summary>Sort keys for the Archived view; the API maps <c>orderBy</c> onto this whitelist only (AU-12).</summary>
public enum ArchivedUserOrder
{
    ArchivedUtc,
    Name,
}
