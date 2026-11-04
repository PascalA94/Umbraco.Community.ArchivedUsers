using Umbraco.Cms.Core.Models.Membership;

namespace Umbraco.Community.ArchivedUsers.Core.Models;

/// <summary>
/// An archived user as the Archived view shows it. <paramref name="ArchivedByName"/> is null for the system or a
/// performer who no longer exists. <paramref name="IsStale"/>: the record exists but the user is not disabled (AU-03).
/// </summary>
public sealed record ArchivedUserView(IUser User, ArchivedUserRecord Record, string? ArchivedByName, bool IsStale);
