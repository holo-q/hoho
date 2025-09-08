using Terminal.Gui;

namespace Hoho;

internal sealed class ChatView : View
{
    private readonly List<(string role, string text)> _messages = new();
    private int _scrollY;
    private (int index, StringBuilder buffer)? _streaming;

    public ChatView()
    {
        CanFocus = true;
    }

    public void AppendUser(string text)
    {
        _messages.Add(("user", text));
        EnsureBottom();
        SetNeedsDisplay();
    }

    public void BeginAssistant()
    {
        _messages.Add(("assistant", string.Empty));
        _streaming = (_messages.Count - 1, new StringBuilder());
        SetNeedsDisplay();
    }

    public void AppendAssistantChunk(string text)
    {
        if (_streaming is { } s)
        {
            s.buffer.Append(text);
            _streaming = (s.index, s.buffer);
            _messages[s.index] = ("assistant", s.buffer.ToString());
            EnsureBottom();
            SetNeedsDisplay();
        }
    }

    public void EndAssistant()
    {
        _streaming = null;
        EnsureBottom();
        SetNeedsDisplay();
    }

    public void AppendInfo(string text)
    {
        _messages.Add(("info", text));
        EnsureBottom();
        SetNeedsDisplay();
    }

    private void EnsureBottom()
    {
        _scrollY = int.MaxValue;
    }

    public override void OnDrawContent(Rect bounds)
    {
        Driver.SetAttribute(ColorScheme.Normal);
        Clear();

        var y = 0 - _scrollY;
        foreach (var (role, text) in _messages)
        {
            var prefix = role switch
            {
                "user" => "You:",
                "assistant" => "Codex:",
                "info" => "Info:",
                _ => "",
            };
            var color = role switch
            {
                "user" => ColorScheme.HotNormal,
                "assistant" => ColorScheme.HotFocus,
                "info" => ColorScheme.Disabled,
                _ => ColorScheme.Normal,
            };

            var lines = WrapWithPrefix(prefix, text, bounds.Width);
            foreach (var line in lines)
            {
                if (y >= 0 && y < bounds.Height)
                {
                    Move(0, y);
                    Driver.SetAttribute(color);
                    Driver.AddStr(line.PadRight(bounds.Width));
                    Driver.SetAttribute(ColorScheme.Normal);
                }
                y++;
            }
            y++; // spacing between messages
        }
    }

    private static IEnumerable<string> WrapWithPrefix(string prefix, string text, int width)
    {
        var avail = Math.Max(1, width);
        var words = (text ?? string.Empty).Replace("\r", string.Empty).Split('\n');
        bool firstLine = true;
        foreach (var para in words)
        {
            var line = new StringBuilder();
            var parts = para.Split(' ');
            foreach (var part in parts)
            {
                var token = (line.Length == 0 ? part : " " + part);
                var pref = firstLine ? prefix + " " : new string(' ', prefix.Length + 1);
                if (pref.Length + line.Length + token.Length > avail)
                {
                    yield return (pref + line.ToString()).PadRight(avail);
                    line.Clear();
                    firstLine = false;
                    token = part; // start new line without leading space
                }
                line.Append(token);
            }
            var prefixNow = firstLine ? prefix + " " : new string(' ', prefix.Length + 1);
            yield return (prefixNow + line.ToString()).PadRight(avail);
            firstLine = false;
        }
    }
}

