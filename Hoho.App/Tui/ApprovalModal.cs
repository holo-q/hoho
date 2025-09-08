using Terminal.Gui;
using Terminal.Gui.Input;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.App;

namespace Hoho;

internal sealed class ApprovalModal : Window
{
    private bool? _result;
    public bool? Result => _result;

    public ApprovalModal(string title, string message)
    {
        Title = title;
        Modal = true;
        Width = Dim.Percent(70);
        Height = Dim.Percent(40);
        X = Pos.Center();
        Y = Pos.Center();

        var msg = new TextView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
            Height = Dim.Fill(3),
            ReadOnly = true,
            WordWrap = true,
            Text = message,
        };

        var yes = new Button { Text = " Yes ", X = Pos.Center() - 6, Y = Pos.AnchorEnd(2) };
        var no  = new Button { Text = " No ",  X = Pos.Center() + 2, Y = Pos.AnchorEnd(2) };

        yes.Accepting += (s,e) => { _result = true; Application.RequestStop(); e.Handled = true; };
        no.Accepting  += (s,e) => { _result = false; Application.RequestStop(); e.Handled = true; };

        Add(msg, yes, no);
        KeyDown += (s, key) =>
        {
            if (key == Key.Enter || key == Key.Y)
            { _result = true; Application.RequestStop(); }
            else if (key == Key.Esc || key == Key.N)
            { _result = false; Application.RequestStop(); }
        };
    }
}
