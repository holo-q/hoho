# TUI Design (Terminal.Gui)

## Layout
- MenuBar + StatusBar (shortcuts, mode, model, workdir).
- Left: Repo Tree / Plans tabs (TabView).
- Center: Transcript (TextView) with streaming updates; message selection.
- Bottom: Composer (TextView) with hint line; supports paste images/files.
- Right: Log pane (ListView) + Approvals queue.

## Keybindings
- Ctrl+N: New session. Ctrl+S: Save transcript. Ctrl+Enter: Send.
- F2: Approvals pane. F5: Run selected plan step. F9: Toggle sandbox mode.
- Esc, Esc: Edit previous user message (backtrack).

## Views
- TranscriptView: render roles, tool calls, diffs (colorized).
- PlanView: show steps, toggle status, add/edit step.
- RepoView: tree with open/preview, search via `@`.
- DiffView: pending changes preview before commit.

## Streaming
- Core raises events (OnToken, OnToolStart/End); UI batches appends to avoid flicker.

