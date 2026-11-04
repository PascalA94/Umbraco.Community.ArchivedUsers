using Umbraco.Cms.Core.Packaging;

namespace Umbraco.Community.ArchivedUsers.Core.Migrations;

/// <summary>The package migration plan. Name and states are permanent once released; later steps are additive only.</summary>
public sealed class ArchivedUsersMigrationPlan : PackageMigrationPlan
{
    public const string PlanName = "Umbraco.Community.ArchivedUsers";

    public const string TablesState = "archived-users-1";

    public ArchivedUsersMigrationPlan()
        : base(PlanName)
    {
    }

    protected override void DefinePlan() => To<AddArchivedUserTables>(TablesState);
}
