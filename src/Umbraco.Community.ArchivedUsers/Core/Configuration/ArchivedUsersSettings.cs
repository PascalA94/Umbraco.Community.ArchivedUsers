namespace Umbraco.Community.ArchivedUsers.Core.Configuration;

/// <summary>Settings bound from the <c>ArchivedUsers</c> configuration section (docs/design.md 7.4).</summary>
/// <remarks>Must be identical on every node of a load-balanced cluster, RequireReason above all (AU-14b).</remarks>
public sealed class ArchivedUsersSettings
{
    public const string SectionName = "ArchivedUsers";

    /// <summary>D1. When false, the <c>/filter/user</c> response is exactly core's.</summary>
    public bool HideArchivedFromUserList { get; set; } = true;

    /// <summary>AU-10. API users (client credentials) get core's list unless this is on.</summary>
    public bool HideArchivedForApiUsers { get; set; }

    /// <summary>Enforced server-side as well as in the confirm dialog.</summary>
    public bool RequireReason { get; set; }

    /// <summary>D3. A request's <c>enable</c> flag can only narrow this, never widen it (AU-11).</summary>
    public bool UnarchiveEnablesUser { get; set; }
}
