using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Community.ArchivedUsers.Core.Persistence;

namespace Umbraco.Community.ArchivedUsers.Core.Migrations;

/// <summary>Creates both package tables if missing. Re-runnable, and tolerant of another node creating them first.</summary>
public sealed class AddArchivedUserTables : AsyncMigrationBase
{
    public AddArchivedUserTables(IMigrationContext context)
        : base(context)
    {
    }

    protected override Task MigrateAsync()
    {
        CreateIfMissing<ArchivedUserDto>(ArchivedUserDto.TableName);
        CreateIfMissing<ArchiveAuditDto>(ArchiveAuditDto.TableName);
        return Task.CompletedTask;
    }

    private void CreateIfMissing<TDto>(string tableName)
    {
        if (TableExists(tableName))
        {
            return;
        }

        try
        {
            Create.Table<TDto>().Do();
        }
        catch (Exception) when (TableExists(tableName))
        {
            // Another node ran the same unattended migration between our check and our create (AU-14b).
            Logger.LogInformation("Table {Table} was created concurrently; continuing", tableName);
        }
    }
}
