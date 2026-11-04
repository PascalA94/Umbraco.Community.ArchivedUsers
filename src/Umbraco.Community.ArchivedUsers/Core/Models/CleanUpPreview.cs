namespace Umbraco.Community.ArchivedUsers.Core.Models;

/// <summary>A user's id and current name, for confirm dialogs. The name is user text: render it as text only (AU-04).</summary>
public sealed record UserNameItem(Guid Id, string Name);

/// <summary>What "Archive disabled users" would archive: the count and up to ten names, ordered by name.</summary>
public sealed record CleanUpPreview(int Count, IReadOnlyList<UserNameItem> Sample);
