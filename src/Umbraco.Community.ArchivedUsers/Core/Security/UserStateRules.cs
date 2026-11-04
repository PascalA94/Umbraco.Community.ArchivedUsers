using Umbraco.Cms.Core.Models.Membership;

namespace Umbraco.Community.ArchivedUsers.Core.Security;

/// <summary>
/// The package's single definition of "disabled". Core's <c>IUser.UserState</c> reports <c>LockedOut</c> before
/// <c>Disabled</c> (User.UserState at release-18.3.0), so a user who is both disabled and locked out would look
/// enabled to a <c>UserState == Disabled</c> test. Leaver accounts are often locked out, so the package tests the
/// approval flag instead (milestone review B, BS-1).
/// </summary>
public static class UserStateRules
{
    /// <summary>Not approved, and not an invitation (invited users are also unapproved but have never been enabled).</summary>
    public static bool IsDisabled(IUser user) => !user.IsApproved && user.UserState != UserState.Invited;

    /// <summary>P9: a performer who is disabled or locked out may change nobody.</summary>
    public static bool CanPerform(IUser? performer) => performer is not null && performer.IsApproved && !performer.IsLockedOut;
}
