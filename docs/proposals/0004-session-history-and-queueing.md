Title: Session History & Prompt Queueing Parity
Status: Draft
Owners: hoho TUI

History
- Cross-session history: ~/.hoho/history.txt (append-only); merged with session-local; no consecutive duplicates.
- Navigation: Up/Down only when caret is at column 0 and text equals last recalled entry; Alt+Up/Down always navigates.
- Backtrack: Esc primes; Esc again shows overlay of last user message; Enter accepts; Esc cancels.

Queueing
- Enter during a running turn enqueues the prompt.
- Status shows queued count and dim previews of first 1–2 queued prompts.
- Drains sequentially after completion.

Implementation Notes
- Hoho.Core/Sessions/HistoryStore.cs stores recent history.
- Hoho.App/Tui/TuiApp.cs merges persistent + local; guards Up/Down.
- StatusIndicatorView dynamically grows for queued previews.

