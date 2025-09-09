using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Terminal.Gui.Input;

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
        _placeholder = new Label
        {
            Text = "Type your message…",
            X = 3,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            Visible = true,
        };
        Add(_text, _placeholder);
        _text.KeyDown += (sender, key) => OnTextKeyDown(key);
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

    public void Clear() => Text = string.Empty;
    public void InsertNewLine() => _text.InsertText("\n");
    public void FocusInner() => _text.SetFocus();

    public event Action<Key>? KeyDownInner;

    public bool IsInPasteBurst => _inBurst || Application.IsInBracketedPaste;

    private void OnTextKeyDown(Key key)
    {
        // Update a simple paste-burst detector: many printable chars in quick succession
        var rune = key.AsRune;
        bool printable = rune.Value != 0 && rune.Value >= 32 && rune.Value <= 126 && !key.IsCtrl && !key.IsAlt;
        if (printable)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastCharAt).TotalMilliseconds <= 20) _burstCount++;
            else _burstCount = 1;
            _lastCharAt = now;
            _inBurst = _burstCount >= 8;
        }
        else
        {
            _inBurst = false;
            _burstCount = 0;
        }
        _placeholder.Visible = string.IsNullOrEmpty(_text.Text?.ToString());
        KeyDownInner?.Invoke(key);
    }

    protected override bool OnDrawingContent()
    {
        // Draw a thick vertical edge on the left (Unicode heavy vertical bar)
        for (int row = 0; row < Viewport.Height; row++)
        {
            Move(0, row);
            Application.Driver?.AddStr("┃");
        }
        return false; // let base draw children (TextView)
    }

    public void InsertText(string s) => _text.InsertText(s);

    public int CaretColumn => _text?.CurrentColumn ?? 0;
}
