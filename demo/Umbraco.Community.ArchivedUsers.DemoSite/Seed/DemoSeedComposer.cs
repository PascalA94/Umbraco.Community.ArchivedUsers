using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Community.ArchivedUsers.DemoSite.Seed;

/// <summary>Registers the demo seeder only when <c>ArchivedUsersDemo:Seed</c> is true (appsettings.Development.json).</summary>
public sealed class DemoSeedComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        if (builder.Config.GetValue<bool>("ArchivedUsersDemo:Seed"))
        {
            builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, DemoUserSeeder>();
        }
    }
}
