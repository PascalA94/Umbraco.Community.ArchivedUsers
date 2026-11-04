using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.ArchivedUsers.Core.Models;
using Umbraco.Community.ArchivedUsers.Core.Persistence;
using Umbraco.Community.ArchivedUsers.Core.Services;

namespace Umbraco.Community.ArchivedUsers.IntegrationTests;

[TestFixture]
public class CleanUpTests : ArchivedUsersIntegrationTestBase
{
    private IArchivedUserService Service => GetRequiredService<IArchivedUserService>();

    [SetUp]
    public async Task Migrate() => await RunPackageMigrationsAsync();

    [Test]
    public async Task Non_admin_preview_excludes_disabled_admins()
    {
        IUser manager = await Users.CreateUserManagerAsync();
        IUser disabledAdmin = await Users.CreateAsync("Former admin", [Constants.Security.AdminGroupAlias], UserState.Disabled);
        IUser disabledEditor = await Users.CreateEditorAsync("Former editor", UserState.Disabled);

        CleanUpPreview preview = await Service.PreviewCleanUpAsync(manager.Key);

        Assert.That(preview.Sample.Select(s => s.Id), Does.Contain(disabledEditor.Key));
        Assert.That(preview.Sample.Select(s => s.Id), Does.Not.Contain(disabledAdmin.Key));
        Assert.That(preview.Count, Is.EqualTo(1));
    }

    /// <summary>T1, AU-01.</summary>
    [Test]
    public async Task Non_admin_clean_up_leaves_disabled_admin_unarchived()
    {
        IUser manager = await Users.CreateUserManagerAsync();
        IUser disabledAdmin = await Users.CreateAsync("Former admin", [Constants.Security.AdminGroupAlias], UserState.Disabled);

        await Service.ArchiveAllDisabledAsync(manager.Key, null);

        using ICoreScope scope = GetRequiredService<ICoreScopeProvider>().CreateCoreScope(autoComplete: true);
        Assert.That(await GetRequiredService<IArchivedUserRepository>().GetAsync(disabledAdmin.Key), Is.Null);
    }
}

/// <summary>F3 with core's HideDisabledUsersInBackOffice on: the count still comes from the server (docs/spikes.md S3).</summary>
[TestFixture]
public class CleanUpWithHideDisabledTests : ArchivedUsersIntegrationTestBase
{
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.Configure<Umbraco.Cms.Core.Configuration.Models.SecuritySettings>(s => s.HideDisabledUsersInBackOffice = true);
    }

    [SetUp]
    public async Task Migrate() => await RunPackageMigrationsAsync();

    [Test]
    public async Task Preview_counts_server_side_when_HideDisabledUsersInBackOffice_is_on()
    {
        IUser admin = await Users.CreateAdminAsync();
        await Users.CreateEditorAsync("Former 1", UserState.Disabled);
        await Users.CreateEditorAsync("Former 2", UserState.Disabled);

        CleanUpPreview preview = await GetRequiredService<IArchivedUserService>().PreviewCleanUpAsync(admin.Key);

        Assert.That(preview.Count, Is.EqualTo(2));
    }
}
