using Umbraco.Community.ArchivedUsers.Core.Models;

namespace Umbraco.Community.ArchivedUsers.Core.Notifications;

/// <summary>Payload of <see cref="UserUnarchivedNotification"/>. <c>ReEnabled</c> is true only when this unarchive enabled the user.</summary>
public sealed record UserUnarchiveEvent(Guid UserKey, Guid? PerformerKey, ArchiveSource Source, bool ReEnabled);
