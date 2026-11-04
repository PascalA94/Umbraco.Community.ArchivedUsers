using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Community.ArchivedUsers.Core.Notifications;

/// <summary>Published after an archive record is removed by any path (package unarchive, core enable, reconciliation).</summary>
public sealed class UserUnarchivedNotification : ObjectNotification<UserUnarchiveEvent>
{
    public UserUnarchivedNotification(UserUnarchiveEvent target, EventMessages messages)
        : base(target, messages)
    {
    }

    public UserUnarchiveEvent Event => Target;
}
