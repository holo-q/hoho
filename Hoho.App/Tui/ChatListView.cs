using Terminal.Gui;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;

namespace Hoho;

internal sealed class ChatListView : View
{
    private readonly VirtualLogView _log;
    private readonly List<string> _lines = new();
    private string _assistantBuffer = string.Empty;
    private bool _assistantStreaming = false;
    private string _lastUserText = string.Empty;

    public ChatListView()
    {
        CanFocus = true;
        _log = new VirtualLogView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), AutoScroll = true };
        Add(_log);
        _log.SetSource(_lines);
    }

    public void AppendUser(string text)
    {
        _lastUserText = text ?? string.Empty;
        AppendMultiline($"You: {_lastUserText}");
    }

    public void BeginAssistant()
    {
        _assistantBuffer = string.Empty;
        _assistantStreaming = true;
        _lines.Add("Codex: ");
        _log.SetNeedsDraw();
    }

    public void AppendAssistantChunk(string text)
    {
        if (!_assistantStreaming) return;
        _assistantBuffer += text ?? string.Empty;
        if (_lines.Count > 0)
        {
            _lines[^1] = "Codex: " + _assistantBuffer.Replace('\r', ' ');
        }
        _log.SetNeedsDraw();
    }

    public void EndAssistant()
    {
        _assistantStreaming = false;
        _log.SetNeedsDraw();
    }

    public void AppendInfo(string text)
    {
        AppendMultiline($"Info: {text}");
    }

    public string GetLastUserMessageText() => _lastUserText;

    private void AppendMultiline(string text)
    {
        var parts = (text ?? string.Empty).Replace("\r", string.Empty).Split('\n');
        foreach (var p in parts) _lines.Add(p);
        _log.ScrollToEnd();
        _log.SetNeedsDraw();
    }
}
