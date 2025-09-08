# Terminal.Gui Mapping

- Transcript: `TextView` (read-only) with color spans; consider `HexView` for binary previews.
- Composer: `TextView` (single-line mode + auto-grow). Autocomplete via `TextViewAutocomplete`.
- Tabs: `TabView` for Files/Plans/Settings.
- Lists: `ListView` for logs, approvals, recent sessions.
- Tree: `TreeView` for repo browser.
- Status: `StatusBar`, `MenuBar` for global commands.
- Progress: `ProgressBar` for long-running tasks.

Patterns: Prefer `Application.MainLoop.Invoke` for thread-safe UI updates from async tasks.

