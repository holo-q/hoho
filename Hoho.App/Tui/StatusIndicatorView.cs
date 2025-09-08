using Terminal.Gui;

namespace Hoho;

internal sealed class StatusIndicatorView : View
{
    private DateTime _startedAt;
    private bool _active;
    private bool _idleHooked;

    public StatusIndicatorView()
    {
        Height = 1;
        CanFocus = false;
    }

    public void Start()
    {
        _startedAt = DateTime.UtcNow;
        _active = true;
        EnsureIdle();
        SetNeedsDisplay();
    }

    public void Stop()
    {
        _active = false;
        SetNeedsDisplay();
    }

    private void EnsureIdle()
    {
        if (_idleHooked) return;
        _idleHooked = true;
        Application.MainLoop.AddIdle(() =>
        {
            if (!_active)
            {
                _idleHooked = false;
                return false;
            }
            SetNeedsDisplay();
            return true;
        });
    }

    private static string FmtElapsed(TimeSpan ts)
    {
        var secs = (long)ts.TotalSeconds;
        if (secs < 60) return $"{secs}s";
        if (secs < 3600) return $"{secs / 60}m{secs % 60:00}s";
        return $"{secs / 3600}h{(secs % 3600) / 60:00}m{secs % 60:00}s";
    }

    public override void OnDrawContent(Rect bounds)
    {
        Clear();
        var now = DateTime.UtcNow;
        var elapsed = _active ? now - _startedAt : TimeSpan.Zero;
        var header = "Working";

        // Shimmer effect: highlight one character position across the header.
        int pos = _active ? (int)((now - _startedAt).TotalMilliseconds / 120) % Math.Max(1, header.Length) : -1;

        // Build status line: " Working (1m02s • Esc to interrupt)"
        int x = 0; int y = 0;
        Move(x, y);
        Driver.AddStr(" "); x++;

        for (int i = 0; i < header.Length; i++)
        {
            if (i == pos)
                Driver.SetAttribute(Application.Driver.MakeAttribute(Color.BrightCyan, Color.Black));
            else
                Driver.SetAttribute(Application.Driver.MakeAttribute(Color.Cyan, Color.Black));
            Driver.AddStr(header[i].ToString());
        }
        Driver.SetAttribute(ColorScheme.Normal);

        var tail = $" ({FmtElapsed(elapsed)} • Esc to interrupt)";
        Driver.AddStr(tail);

        // Pad rest of line
        var remaining = bounds.Width - (x + header.Length + tail.Length);
        if (remaining > 0)
        {
            Driver.AddStr(new string(' ', remaining));
        }
    }
}
