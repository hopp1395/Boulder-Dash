namespace BoulderDash.Tests;

/// <summary>Findet Repo-relative Pfade unabhängig vom Arbeitsverzeichnis des Testrunners.</summary>
internal static class TestPaths
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string GameAssets => Path.Combine(RepoRoot, "csharp", "BoulderDash.Game", "Assets");

    public static string SrcRoot => Path.Combine(RepoRoot, "src");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo-Wurzel (CLAUDE.md) nicht gefunden.");
    }
}
