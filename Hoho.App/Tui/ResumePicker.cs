using Hoho.Core.Sessions;
using Terminal.Gui;

namespace Hoho;

internal static class ResumePicker
{
    public static string? Show()
    {
        var dlg = new Window("Resume Session")
        {
            Modal = true,
            Width = Dim.Percent(80),
            Height = Dim.Percent(60),
            X = Pos.Center(),
            Y = Pos.Center(),
        };
        var list = new ListView() { X = 1, Y = 1, Width = Dim.Fill(2), Height = Dim.Fill(2) };
        var sessions = SessionDiscovery.ListSessions(50).ToList();
        var items = sessions.Select(s =>
        {
            var prev = SessionDiscovery.FirstUserPreview(s.Id) ?? "(no preview)";
            return $"{s.Id}  -  {prev}";
        }).ToList();
        list.SetSource(items);
        string? selected = null;
        list.OpenSelectedItem += i => { selected = sessions[i.Item].Id; Application.RequestStop(); };

        dlg.Add(list);
        Application.Run(dlg);
        return selected;
    }
}

