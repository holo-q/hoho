Title: Zen UI Parity — Minimal Movement & Calm Cadence
Status: Draft
Owners: hoho TUI

Principles
- Non‑streaming: render assistant message once at completion.
- Minimal motion: no spinner; shimmer header + elapsed only.
- Idle redraw: refresh only while a turn is active.
- Stable layout: status grows only to show queued previews; collapses afterwards.
- Quiet interactions: Esc interrupt; Esc–Esc backtrack when composer empty; paste‑burst disables popups.

Hoho Implementation
- StatusIndicatorView: cyan shimmer, compact elapsed, Esc hint, dynamic height.
- ChatView: single column chat with role prefixes (You/Codex/Info); prefix color only on first line.
- ComposerView: cyan gutter; Shift+Enter newline; Enter submit; Alt+Up/Down history; paste-burst detection.
- Queueing: Enter during run queues prompt; queued count + dim sublines (capped) under status.

Do Nots
- No frantic token streaming; no busy spinners; no popping modals during paste.

Paths
- Hoho.App/Tui/StatusIndicatorView.cs
- Hoho.App/Tui/ChatView.cs
- Hoho.App/Tui/ComposerView.cs
- Hoho.App/Tui/TuiApp.cs

