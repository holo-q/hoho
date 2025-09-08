using Terminal.Gui;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using System.Text;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;

namespace Hoho;

internal sealed class ChatView : View
{
	private readonly List<(string role, string text)>   _messages = new();
	private          int                                _scrollY;
	private          (int index, StringBuilder buffer)? _streaming;

	public ChatView()
	{
		CanFocus = true;
	}

	public void AppendUser(string text)
	{
		_messages.Add(("user", text));
		EnsureBottom();
		SetNeedsDraw();
	}

	public void BeginAssistant()
	{
		_messages.Add(("assistant", string.Empty));
		_streaming = (_messages.Count - 1, new StringBuilder());
		SetNeedsDraw();
	}

	public void AppendAssistantChunk(string text)
	{
		if (_streaming is { } s)
		{
			s.buffer.Append(text);
			_streaming         = (s.index, s.buffer);
			_messages[s.index] = ("assistant", s.buffer.ToString());
			EnsureBottom();
			SetNeedsDraw();
		}
	}

	public void EndAssistant()
	{
		_streaming = null;
		EnsureBottom();
		SetNeedsDraw();
	}

	public void AppendInfo(string text)
	{
		_messages.Add(("info", text));
		EnsureBottom();
		SetNeedsDraw();
	}

	public string GetLastUserMessageText()
	{
		for (int i = _messages.Count - 1; i >= 0; i--)
		{
			if (_messages[i].role == "user") return _messages[i].text;
		}
		return string.Empty;
	}

	private void EnsureBottom()
	{
		_scrollY = int.MaxValue;
	}

	protected override bool OnDrawingContent()
	{
		SetAttribute(GetAttributeForRole(VisualRole.Normal));

		var width  = Viewport.Width;
		var height = Viewport.Height;
		var y      = 0 - _scrollY;
		foreach (var (role, text) in _messages)
		{
			var prefix = role switch
			{
				"user"      => "You:",
				"assistant" => "Codex:",
				"info"      => "Info:",
				_           => "",
			};
			// Render without per-prefix color for now; reintroduce once v2 styling is finalized

			var  lines = WrapWithPrefix(prefix, text, width);
			bool first = true;
			foreach (var line in lines)
			{
				if (y >= 0 && y < height)
				{
					Move(0, y);
					var display = line.PadRight(width);
					if (first)
					{
						// Color only the prefix on the first line
						var prefLen = Math.Min(prefix.Length + 1, display.Length);
						Application.Driver?.AddStr(display.Substring(0, prefLen));
						SetAttribute(GetAttributeForRole(VisualRole.Normal));
						Application.Driver?.AddStr(display.Substring(prefLen));
					}
					else
					{
						// Subsequent lines: plain text (indented spaces already present)
						Application.Driver?.AddStr(display);
					}
					SetAttribute(GetAttributeForRole(VisualRole.Normal));
				}
				y++;
				first = false;
			}
			y++; // spacing between messages
		}
		return true;
	}

	private static IEnumerable<string> WrapWithPrefix(string prefix, string text, int width)
	{
		var  avail     = Math.Max(1, width);
		var  words     = (text ?? string.Empty).Replace("\r", string.Empty).Split('\n');
		bool firstLine = true;
		foreach (var para in words)
		{
			var line  = new StringBuilder();
			var parts = para.Split(' ');
			foreach (var part in parts)
			{
				var token = (line.Length == 0 ? part : " " + part);
				var pref  = firstLine ? prefix + " " : new string(' ', prefix.Length + 1);
				if (pref.Length + line.Length + token.Length > avail)
				{
					yield return (pref + line.ToString()).PadRight(avail);
					line.Clear();
					firstLine = false;
					token     = part; // start new line without leading space
				}
				line.Append(token);
			}
			var prefixNow = firstLine ? prefix + " " : new string(' ', prefix.Length + 1);
			yield return (prefixNow + line.ToString()).PadRight(avail);
			firstLine = false;
		}
	}
}