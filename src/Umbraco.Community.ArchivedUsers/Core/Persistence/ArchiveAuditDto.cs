using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Umbraco.Community.ArchivedUsers.Core.Persistence;

/// <summary>Table <c>archivedUsersAudit</c>. No foreign keys, so rows outlive the users they describe (D7, GDPR note).</summary>
[TableName(TableName)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
internal sealed class ArchiveAuditDto
{
    public const string TableName = "archivedUsersAudit";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("userKey")]
    [Index(IndexTypes.NonClustered, Name = "IX_" + TableName + "_userKey")]
    public Guid UserKey { get; set; }

    [Column("action")]
    [Length(32)]
    public string Action { get; set; } = string.Empty;

    [Column("performedByUserKey")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public Guid? PerformedByUserKey { get; set; }

    [Column("performedUtc")]
    [Index(IndexTypes.NonClustered, Name = "IX_" + TableName + "_performedUtc")]
    public DateTime PerformedUtc { get; set; }

    [Column("source")]
    [Length(32)]
    public string Source { get; set; } = string.Empty;

    [Column("reason")]
    [Length(500)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Reason { get; set; }

    [Column("userNameSnapshot")]
    [Length(255)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? UserNameSnapshot { get; set; }

    [Column("reEnabled")]
    [Constraint(Default = "0")]
    public bool ReEnabled { get; set; }

    [Column("context")]
    [Length(255)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Context { get; set; }
}
