using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Community.ArchivedUsers.Core.Notifications;

/// <summary>
/// Published before a user is archived; a handler may cancel it. Handlers must not call the archive service
/// for the same user: the per-user lock is held and is not re-entrant (docs/design.md 7.2, P6).
/// </summary>
public sealed class UserArchivingNotification : CancelableObjectNotification<UserArchiveEvent>
{
    public UserArchivingNotification(UserArchiveEvent target, EventMessages messages)
        : base(target, messages)
    {
    }

    public UserArchiveEvent Event => Target;
}
