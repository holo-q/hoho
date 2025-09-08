namespace Hoho.Core.Guidance;

public static class AgentsLoader
{
    public static string LoadMergedAgents(string workdir)
    {
        // Merge order: ~/.codex/AGENTS.md, <repo-root>/AGENTS.md, <workdir>/AGENTS.md
        var parts = new List<string>();
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var global = Path.Combine(home, ".codex", "AGENTS.md");
            if (File.Exists(global)) parts.Add(File.ReadAllText(global));
        }
        catch { }

        TryAdd(workdir, "AGENTS.md", parts);

        // If the workdir is a subfolder in a repo, also look up the tree until root
        var dir = new DirectoryInfo(workdir);
        while (dir is not null)
        {
            var p = Path.Combine(dir.FullName, "AGENTS.md");
            if (File.Exists(p)) parts.Add(File.ReadAllText(p));
            dir = dir.Parent;
        }

        return string.Join("\n\n", parts.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
    }

    private static void TryAdd(string baseDir, string rel, List<string> parts)
    {
        try
        {
            var p = Path.Combine(baseDir, rel);
            if (File.Exists(p)) parts.Add(File.ReadAllText(p));
        }
        catch { }
    }
}

