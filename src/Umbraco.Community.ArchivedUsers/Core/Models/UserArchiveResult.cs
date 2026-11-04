using Umbraco.Cms.Core.Services.OperationStatus;

namespace Umbraco.Community.ArchivedUsers.Core.Models;

/// <summary>
/// One user's result. <paramref name="CoreStatus"/> is set when core refused; <paramref name="Message"/> carries
/// a cancel message from a notification handler (rendered as text only, AU-04).
/// </summary>
public sealed record UserArchiveResult(Guid Id, ArchiveResultStatus Status, UserOperationStatus? CoreStatus = null, string? Message = null);
