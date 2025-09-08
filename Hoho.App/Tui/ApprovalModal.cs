using Terminal.Gui;

namespace Hoho;

internal sealed class ApprovalModal : Window
{
    private bool? _result;
    public bool? Result => _result;

    public ApprovalModal(string title, string message)
        : base(title, 0)
    {
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

        var yes = new Button(" Yes ") { X = Pos.Center() - 6, Y = Pos.AnchorEnd(2) };
        var no  = new Button(" No ")  { X = Pos.Center() + 2, Y = Pos.AnchorEnd(2) };

        yes.Clicked += () => { _result = true; Application.RequestStop(); };
        no.Clicked  += () => { _result = false; Application.RequestStop(); };

        Add(msg, yes, no);
        KeyPress += e =>
        {
            if (e.KeyEvent.Key == Key.Enter || e.KeyEvent.Key == Key.Y)
            { _result = true; e.Handled = true; Application.RequestStop(); }
            else if (e.KeyEvent.Key == Key.Esc || e.KeyEvent.Key == Key.N)
            { _result = false; e.Handled = true; Application.RequestStop(); }
        };
    }
}

