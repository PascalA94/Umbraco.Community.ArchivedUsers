using Umbraco.Cms.Api.Management.ViewModels.User;

namespace Umbraco.Community.ArchivedUsers.Web.Controllers.Models;

// Request models carry exactly these fields. Performer, source and times are derived on the server and are
// never bound from a request, so extra body fields such as source or performerKey are ignored (AU-12).

public sealed class ArchiveUsersRequestModel
{
    public Guid[] UserIds { get; set; } = [];

    public string? Reason { get; set; }
}

public sealed class UnarchiveUsersRequestModel
{
    public Guid[] UserIds { get; set; } = [];

    /// <summary>Can only narrow the site's UnarchiveEnablesUser setting; true while the setting is off is a 400 (AU-11).</summary>
    public bool? Enable { get; set; }
}

public sealed class CleanUpRequestModel
{
    public string? Reason { get; set; }
}

public sealed class UserArchiveResultResponseModel
{
    public required Guid Id { get; init; }

    /// <summary>
    /// A status name (including <c>DisabledNotArchived</c> and <c>Failed</c> from 0.2), or
    /// <c>CoreRefused:&lt;UserOperationStatus&gt;</c>.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>A cancel message from a notification handler. User text: render as text only (AU-04).</summary>
    public string? Message { get; init; }
}

public sealed class ArchivedByResponseModel
{
    public required Guid Id { get; init; }

    /// <summary>Null when the archiving user no longer exists ("Removed user").</summary>
    public string? Name { get; init; }
}

public sealed class ArchivedUserResponseModel
{
    /// <summary>Core's user model, nested rather than copied, so it never drifts from core.</summary>
    public required UserResponseModel User { get; init; }

    public required DateTime ArchivedUtc { get; init; }

    /// <summary>Null means the system archived the user.</summary>
    public ArchivedByResponseModel? ArchivedBy { get; init; }

    public string? Reason { get; init; }

    public required string Source { get; init; }

    /// <summary>The record exists but the user is enabled (AU-03).</summary>
    public required bool Stale { get; init; }
}

public sealed class UserNameResponseModel
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }
}

public sealed class CleanUpPreviewResponseModel
{
    public required int Count { get; init; }

    public required UserNameResponseModel[] Sample { get; init; }
}

public sealed class ArchivedUsersConfigResponseModel
{
    public required bool HideArchivedFromUserList { get; init; }

    public required bool RequireReason { get; init; }

    public required bool UnarchiveEnablesUser { get; init; }

    /// <summary>The client has no constant for these; the UI uses them to hide actions only (docs/spikes.md S6).</summary>
    public required Guid AdminGroupId { get; init; }

    public required Guid SuperUserId { get; init; }
}
