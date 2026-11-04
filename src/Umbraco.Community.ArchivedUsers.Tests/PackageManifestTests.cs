using System.Text.Json;

namespace Umbraco.Community.ArchivedUsers.Tests;

public class PackageManifestTests
{
    /// <summary>The package name and bundle alias are permanent once released (docs/plan.md, Global Constraints).</summary>
    [Fact]
    public void Manifest_identity_is_permanent()
    {
        var path = Path.Combine(RepoPaths.PackageProjectDirectory, "wwwroot", "umbraco-package.json");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        Assert.Equal("Umbraco.Community.ArchivedUsers", root.GetProperty("name").GetString());
        var extensions = root.GetProperty("extensions").EnumerateArray().ToList();
        var bundle = Assert.Single(extensions);
        Assert.Equal("bundle", bundle.GetProperty("type").GetString());
        Assert.Equal("ArchivedUsers.Bundle", bundle.GetProperty("alias").GetString());
        Assert.Equal("/App_Plugins/ArchivedUsers/index.js", bundle.GetProperty("js").GetString());
    }
}
