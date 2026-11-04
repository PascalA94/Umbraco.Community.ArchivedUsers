using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Community.ArchivedUsers.Core.Notifications;

/// <summary>
/// Published before a user is archived; a handler may cancel it. Handlers must not call the archive service at
/// all: the per-user lock is held, is not re-entrant, and is striped across users (docs/design.md 7.2, P6, review BS-3).
/// </summary>
public sealed class UserArchivingNotification : CancelableObjectNotification<UserArchiveEvent>
{
    public UserArchivingNotification(UserArchiveEvent target, EventMessages messages)
        : base(target, messages)
    {
    }

    public UserArchiveEvent Event => Target;
}
