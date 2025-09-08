using System.Text;

namespace Hoho.Core.Sessions;

public sealed class HistoryStore
{
    private readonly string _path;
    public HistoryStore(string? path = null)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var codir = Path.Combine(home, ".hoho");
        Directory.CreateDirectory(codir);
        _path = path ?? Path.Combine(codir, "history.txt");
        if (!File.Exists(_path)) File.WriteAllText(_path, "");
    }

    public IReadOnlyList<string> LoadRecent(int limit = 200)
    {
        try
        {
            var lines = File.ReadAllLines(_path, Encoding.UTF8);
            return lines.Reverse().Take(limit).Reverse().ToList();
        }
        catch { return Array.Empty<string>(); }
    }

    public void Append(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            using var sw = new StreamWriter(_path, append: true, Encoding.UTF8);
            sw.WriteLine(text.Replace('\r', ' ').Replace('\n', ' '));
        }
        catch { /* ignore */ }
    }
}

