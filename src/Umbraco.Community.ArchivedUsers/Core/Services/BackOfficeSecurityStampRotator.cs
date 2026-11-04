using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Community.ArchivedUsers.Core.Services;

/// <summary>
/// Rotates the stamp through core identity. <c>IBackOfficeUserManager</c> does not declare
/// <c>UpdateSecurityStampAsync</c>; its registered implementation derives from
/// <c>UserManager&lt;BackOfficeIdentityUser&gt;</c>, which does. Works without an HttpContext
/// (docs/spikes.md S4, <c>CoreContractTests.UpdateSecurityStampAsync_changes_stamp_without_HttpContext</c>).
/// A fresh DI scope keeps the identity user manager's state out of the caller's scope.
/// </summary>
internal sealed class BackOfficeSecurityStampRotator : ISecurityStampRotator
{
    private readonly IServiceScopeFactory _scopeFactory;

    public BackOfficeSecurityStampRotator(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task RotateAsync(Guid userKey)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IBackOfficeUserManager>();
        BackOfficeIdentityUser user = await manager.FindByIdAsync(userKey.ToString())
            ?? throw new InvalidOperationException("User not found for security stamp rotation.");

        if (manager is not UserManager<BackOfficeIdentityUser> userManager)
        {
            throw new InvalidOperationException("The registered back-office user manager cannot rotate security stamps.");
        }

        IdentityResult result = await userManager.UpdateSecurityStampAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Security stamp rotation failed: " + string.Join(",", result.Errors.Select(e => e.Code)));
        }
    }
}
