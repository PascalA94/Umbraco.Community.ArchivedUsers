using System.Reflection;
using Umbraco.Community.ArchivedUsers.Web.Filters;

namespace Umbraco.Community.ArchivedUsers.Tests;

public class ScaffoldTests
{
    // Pins the build switch: a mis-wired UmbracoMajor would otherwise pack one line's code under another line's version.
    [Fact]
    public void Assembly_TargetsSelectedMajor()
    {
#if UMBRACO_17
        const int expected = 17;
#elif UMBRACO_19
        const int expected = 19;
#else
        const int expected = 18;
#endif
        var info = typeof(ArchivedUserListFilter).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        Assert.StartsWith($"{expected}.", info);
    }
}
