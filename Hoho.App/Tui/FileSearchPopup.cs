using System.Collections.ObjectModel;
using Terminal.Gui;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.App;

namespace Hoho;

internal static class FileSearchPopup
{
    public static string? Show(string workdir)
    {
        var dlg = new Window
        {
            Title = "Insert file path",
            Modal = true,
            Width = Dim.Percent(80),
            Height = Dim.Percent(60),
            X = Pos.Center(),
            Y = Pos.Center(),
        };

        var filter = new TextField { Text = "", X = 1, Y = 0, Width = Dim.Fill(2) };
        var list = new ListView() { X = 1, Y = 2, Width = Dim.Fill(2), Height = Dim.Fill(2) };
        var hint = new Label { Text = "Type to filter; Enter to insert; Esc to cancel", X = 1, Y = 1 };

        // Collect files (limit for performance)
        var all = EnumerateRelPaths(workdir, max: 3000).ToList();
        var filtered = new ObservableCollection<string>(all);
        list.SetSource(filtered);

        void ApplyFilter()
        {
            var q = (filter.Text?.ToString() ?? string.Empty).Trim();
            filtered.Clear();
            if (string.IsNullOrEmpty(q))
            {
                foreach (var p in all) filtered.Add(p);
            }
            else
            {
                foreach (var p in all)
                {
                    if (p.Contains(q, StringComparison.OrdinalIgnoreCase))
                        filtered.Add(p);
                }
            }
            list.MoveHome();
            list.SetNeedsDraw();
        }

        filter.TextChanged += (s,e) => ApplyFilter();
        string? selected = null;
        list.OpenSelectedItem += (sender, args) =>
        {
            if (args.Item >= 0 && args.Item < filtered.Count) selected = filtered[args.Item];
            Application.RequestStop();
        };

        dlg.Add(filter, hint, list);
        Application.Run(dlg);
        return selected;
    }

    private static IEnumerable<string> EnumerateRelPaths(string root, int max)
    {
        var rootFull = Path.GetFullPath(root);
        var stack = new Stack<string>();
        stack.Push(rootFull);
        int count = 0;
        while (stack.Count > 0 && count < max)
        {
            var dir = stack.Pop();
            IEnumerable<string> entries;
            try { entries = Directory.EnumerateFileSystemEntries(dir); } catch { continue; }
            foreach (var e in entries)
            {
                var rel = Path.GetRelativePath(rootFull, e).Replace('\\', '/');
                if (Directory.Exists(e))
                {
                    if (!rel.StartsWith(".git") && !rel.StartsWith(".venv") && !rel.StartsWith("bin") && !rel.StartsWith("obj"))
                        stack.Push(e);
                }
                else
                {
                    yield return rel;
                    count++;
                    if (count >= max) yield break;
                }
            }
        }
    }
}
