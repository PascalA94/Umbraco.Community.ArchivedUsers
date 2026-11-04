namespace Umbraco.Community.ArchivedUsers.Tests;

/// <summary>Resolves repository paths from the test assembly location, so tests run from any working directory.</summary>
internal static class RepoPaths
{
    public static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    public static string PackageProjectDirectory => Path.Combine(RepoRoot, "src", "Umbraco.Community.ArchivedUsers");
}
