#nullable enable
namespace Terminal.Gui.App;

/// <summary>
/// Options that control terminal behavior and input features for the Application.
/// </summary>
public sealed class ApplicationOptions
{
    /// <summary>
    /// Use the terminal's alternate screen buffer (xterm 1049/1047). When true, the UI runs without polluting scrollback.
    /// Defaults to false to preserve scrollback by default.
    /// </summary>
    public bool UseAlternateScreenBuffer { get; init; } = false;

    /// <summary>
    /// Mouse tracking level. Defaults to Basic (click/drag). None disables mouse sequences entirely.
    /// </summary>
    public MouseTrackingMode MouseTracking { get; init; } = MouseTrackingMode.Basic;

    /// <summary>
    /// If true, attempt to restore common console modes (wrap, origin, margins, cursor, mouse) on shutdown.
    /// </summary>
    public bool RestoreConsoleOnExit { get; init; } = true;

    /// <summary>
    /// If true, clear the screen on init. This does not use scrollback-clearing sequences.
    /// </summary>
    public bool ClearOnInit { get; init; } = false;
}

public enum MouseTrackingMode
{
    None,
    Basic,      // click/drag (currently mapped to enabling general mouse events)
    AnyEvent,   // high-frequency movement events
    Sgr         // SGR encoding; applies in combination with above where supported
}

