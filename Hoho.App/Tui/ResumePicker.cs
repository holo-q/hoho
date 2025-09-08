using Hoho.Core.Sessions;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Hoho;

internal static class ResumePicker
{
    public static string? Show()
    {
        var dlg = new Window
        {
            Title = "Resume Session",
            Modal = true,
            Width = Dim.Percent(80),
            Height = Dim.Percent(60),
            X = Pos.Center(),
            Y = Pos.Center(),
        };
        var list = new ListView() { X = 1, Y = 1, Width = Dim.Fill(2), Height = Dim.Fill(2) };
        var sessions = SessionDiscovery.ListSessions(50).ToList();
        var itemsList = sessions.Select(s =>
        {
            var prev = SessionDiscovery.FirstUserPreview(s.Id) ?? "(no preview)";
            return $"{s.Id}  -  {prev}";
        }).ToList();
        var items = new System.Collections.ObjectModel.ObservableCollection<string>(itemsList);
        list.SetSource<string>(items);
        string? selected = null;
        list.OpenSelectedItem += (s, i) => { selected = sessions[i.Item].Id; Application.RequestStop(); };

        dlg.Add(list);
        Application.Run(dlg);
        return selected;
    }
}
