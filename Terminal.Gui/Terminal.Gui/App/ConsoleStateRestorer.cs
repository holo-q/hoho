#nullable enable
namespace Terminal.Gui.App;

internal static class ConsoleStateRestorer
{
    public static void Restore()
    {
        // Only attempt general VT resets on VT-enabled terminals. Fallback harmless if unsupported.
        // Do NOT clear scrollback. We only restore modes.
        var seq = string.Concat(
            "\x1b[m",       // SGR0
            "\x1b[?7h",    // DECAWM on (wrap)
            "\x1b[?6l",    // DECOM off (absolute origin)
            "\x1b[r",       // DECSTBM reset (full scroll region)
            "\x1b[?69l",    // DECSLRM off (left/right margins)
            "\x1b[?25h"     // Show cursor
        );
        var mouseOff = string.Concat(
            "\x1b[?1000l", // X10
            "\x1b[?1002l", // Button event
            "\x1b[?1003l", // Any event
            "\x1b[?1006l", // SGR
            "\x1b[?1015l"  // URXVT
        );
        try { Console.Write(seq); } catch { }
        try { Console.Error.Write(seq); } catch { }
        try { Console.Write(mouseOff); } catch { }
        try { Console.Error.Write(mouseOff); } catch { }
    }
}

