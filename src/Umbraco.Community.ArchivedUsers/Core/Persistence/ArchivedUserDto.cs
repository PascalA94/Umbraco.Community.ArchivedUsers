using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Umbraco.Community.ArchivedUsers.Core.Persistence;

/// <summary>
/// Table <c>archivedUsersUser</c>. Table and column names are permanent once released.
/// No foreign key to <c>umbracoUser</c> on purpose: audit outlives the user, and an FK would block
/// core user deletes (docs/design.md 7.2; Umbraco issue #23987).
/// </summary>
[TableName(TableName)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
internal sealed class ArchivedUserDto
{
    public const string TableName = "archivedUsersUser";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("userKey")]
    [Index(IndexTypes.UniqueNonClustered, Name = "IX_" + TableName + "_userKey")]
    public Guid UserKey { get; set; }

    [Column("archivedUtc")]
    public DateTime ArchivedUtc { get; set; }

    [Column("archivedByUserKey")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public Guid? ArchivedByUserKey { get; set; }

    [Column("source")]
    [Length(32)]
    public string Source { get; set; } = string.Empty;

    [Column("reason")]
    [Length(500)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Reason { get; set; }

    [Column("wasDisabledBefore")]
    public bool WasDisabledBefore { get; set; }
}
