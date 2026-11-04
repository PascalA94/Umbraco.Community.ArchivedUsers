namespace Umbraco.Community.ArchivedUsers.Core.Services;

/// <summary>Rotates a user's security stamp so existing sessions end (AU-02).</summary>
internal interface ISecurityStampRotator
{
    Task RotateAsync(Guid userKey);
}
