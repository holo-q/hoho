using Terminal.Gui;

namespace Hoho;

internal sealed class BacktrackPreview : Window
{
    private readonly Label _title;
    private readonly TextView _preview;
    public event Action? Accepted;
    public event Action? Canceled;

    public BacktrackPreview(string content)
        : base("Edit previous message", 0)
    {
        Modal = true;
        Width = Dim.Percent(80);
        Height = Dim.Percent(40);
        X = Pos.Center();
        Y = Pos.AnchorEnd(5);

        _title = new Label("Press Enter to edit; Esc to cancel")
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(2),
            Height = 1,
        };
        _preview = new TextView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
            Height = Dim.Fill(1),
            ReadOnly = true,
            Text = content ?? string.Empty,
            WordWrap = true,
        };

        Add(_title, _preview);
        KeyPress += e =>
        {
            if (e.KeyEvent.Key == Key.Enter)
            {
                e.Handled = true;
                Accepted?.Invoke();
            }
            else if (e.KeyEvent.Key == Key.Esc)
            {
                e.Handled = true;
                Canceled?.Invoke();
            }
        };
    }
}

