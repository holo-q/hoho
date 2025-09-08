using Terminal.Gui;

namespace Hoho;

internal sealed class ComposerView : View
{
    private readonly TextView _text;
    public ComposerView()
    {
        CanFocus = true;
        _text = new TextView
        {
            X = 2,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            WordWrap = false,
            ReadOnly = false,
        };
        Add(_text);
        _text.KeyPress += (e) => OnKeyPress(e);
    }

    public string Text
    {
        get => _text.Text.ToString();
        set => _text.Text = value ?? string.Empty;
    }

    public void Clear() => _text.Text = string.Empty;
    public void InsertNewLine() => _text.InsertText("\n");
    public void FocusInner() => _text.SetFocus();

    public event Action<KeyEventEventArgs>? KeyPressInner;
    protected override bool OnKeyDown(Key key)
    {
        return base.OnKeyDown(key);
    }

    private void OnKeyPress(KeyEventEventArgs e)
    {
        KeyPressInner?.Invoke(e);
    }

    public override void OnDrawContent(Rect bounds)
    {
        // Draw a thick vertical edge on the left (Unicode heavy vertical bar)
        Move(0, 0);
        var attr = Application.Driver.MakeAttribute(Color.Cyan, Color.Black);
        Driver.SetAttribute(attr);
        for (int row = 0; row < bounds.Height; row++)
        {
            Move(0, row);
            Driver.AddStr("┃");
        }
        Driver.SetAttribute(ColorScheme.Normal);
        base.OnDrawContent(bounds);
    }
}

