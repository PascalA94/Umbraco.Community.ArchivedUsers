using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Community.ArchivedUsers.Core.Notifications;

/// <summary>Published after the archive record and audit row are committed.</summary>
public sealed class UserArchivedNotification : ObjectNotification<UserArchiveEvent>
{
    public UserArchivedNotification(UserArchiveEvent target, EventMessages messages)
        : base(target, messages)
    {
    }

    public UserArchiveEvent Event => Target;
}
