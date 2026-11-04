using Umbraco.Community.ArchivedUsers.Core.Configuration;

namespace Umbraco.Community.ArchivedUsers.Tests;

public class ArchivedUsersSettingsTests
{
    [Fact]
    public void Defaults_match_spec()
    {
        var settings = new ArchivedUsersSettings();

        Assert.True(settings.HideArchivedFromUserList);
        Assert.False(settings.HideArchivedForApiUsers);
        Assert.False(settings.RequireReason);
        Assert.False(settings.UnarchiveEnablesUser);
        Assert.Equal("ArchivedUsers", ArchivedUsersSettings.SectionName);
    }
}
