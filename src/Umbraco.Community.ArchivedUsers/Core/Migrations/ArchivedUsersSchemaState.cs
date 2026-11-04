using Umbraco.Cms.Core.Services;

namespace Umbraco.Community.ArchivedUsers.Core.Migrations;

/// <summary>
/// Whether the package tables exist yet. On a fresh install core saves users (and the site starts) before the
/// package migration has run; querying the missing tables then makes core log SQL errors. Core's upgrader stores
/// the plan's state once it has run. A DI singleton: once true it stays true, so later checks cost nothing.
/// </summary>
internal sealed class ArchivedUsersSchemaState
{
    private const string MigrationStateKey = "Umbraco.Core.Upgrader.State+" + ArchivedUsersMigrationPlan.PlanName;

    private readonly IKeyValueService _keyValues;
    private volatile bool _ready;

    public ArchivedUsersSchemaState(IKeyValueService keyValues) => _keyValues = keyValues;

    public bool IsReady()
    {
        if (!_ready && _keyValues.GetValue(MigrationStateKey) is not null)
        {
            _ready = true;
        }

        return _ready;
    }
}
