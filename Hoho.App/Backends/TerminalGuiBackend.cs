using Terminal.Gui.App;

namespace Hoho;

internal sealed class TerminalGuiBackend : ITuiBackend
{
    public int Run(string workdir, string providerName, string? sessionId, string? initialPrompt = null, int? smokeMs = null)
    {
        // Ensure sane defaults for Terminal.Gui options
        Application.Init(driver: null, driverName: "NetDriver", options: new ApplicationOptions
        {
            UseAlternateScreenBuffer = false,
            MouseTracking = MouseTrackingMode.None,
            RestoreConsoleOnExit = true,
            ClearOnInit = true,
            EnableBracketedPaste = true,
        });

        return TuiApp.Run(workdir, providerName, sessionId, initialPrompt, smokeMs);
    }
}

