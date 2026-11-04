using Umbraco.Community.ArchivedUsers.Core.Models;

namespace Umbraco.Community.ArchivedUsers.Core.Notifications;

/// <summary>Payload of the archiving and archived notifications. <c>PerformerKey</c> null means the system.</summary>
public sealed record UserArchiveEvent(Guid UserKey, Guid? PerformerKey, ArchiveSource Source, string? Reason);
