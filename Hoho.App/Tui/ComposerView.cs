using Terminal.Gui;

namespace Hoho;

internal sealed class ComposerView : View
{
    private readonly TextView _text;
    private DateTime _lastCharAt = DateTime.MinValue;
    private int _burstCount = 0;
    private bool _inBurst = false;

    private readonly Label _placeholder;

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
        _placeholder = new Label("Type your message…")
        {
            X = 3,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            Visible = true,
            ColorScheme = new ColorScheme { Normal = Application.Driver.MakeAttribute(Color.Gray, Color.Black) },
        };
        Add(_text, _placeholder);
        _text.KeyPress += (e) => OnKeyPress(e);
    }

    public string Text
    {
        get => _text.Text.ToString();
        set
        {
            _text.Text = value ?? string.Empty;
            _placeholder.Visible = string.IsNullOrEmpty(_text.Text?.ToString());
        }
    }

    public void Clear() => _text.Text = string.Empty;
    public void InsertNewLine() => _text.InsertText("\n");
    public void FocusInner() => _text.SetFocus();

    public event Action<KeyEventEventArgs>? KeyPressInner;
    protected override bool OnKeyDown(Key key)
    {
        return base.OnKeyDown(key);
    }

    public bool IsInPasteBurst => _inBurst;

    private void OnKeyPress(KeyEventEventArgs e)
    {
        // Update a simple paste-burst detector: many printable chars in quick succession
        if (IsPrintableChar(e) && (e.KeyEvent.KeyModifiers & (KeyModifiers.Ctrl | KeyModifiers.Alt)) == 0)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastCharAt).TotalMilliseconds <= 25)
            {
                _burstCount++;
            }
            else
            {
                _burstCount = 1;
            }
            _lastCharAt = now;
            _inBurst = _burstCount >= 5; // enter burst after a few rapid chars
        }
        else
        {
            _inBurst = false;
            _burstCount = 0;
        }
        _placeholder.Visible = string.IsNullOrEmpty(_text.Text?.ToString());
        KeyPressInner?.Invoke(e);
    }

    private static bool IsPrintableChar(KeyEventEventArgs e)
    {
        var code = (uint)e.KeyEvent.Key;
        if (code > 0x7E) return false;
        char c = (char)code;
        return c >= ' ' && c <= '~';
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

    public void InsertText(string s) => _text.InsertText(s);
}
