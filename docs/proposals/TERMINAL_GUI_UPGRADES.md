# Proposal: Terminal.Gui v2 Upgrades (Parking Lot)

Status: parked (Ratatui backend prioritized)

This document captures the remaining Terminal.Gui v2 improvements we planned and partially implemented. It’s intended as a high‑quality blueprint so we (or the community) can pick up the work later without rediscovery.

## Goals

- Improve terminal hygiene, performance, and UX to “Ratatui‑class”.
- Keep changes upstream‑friendly: options‑gated, additive, and tested.
- Provide pragmatic components for large outputs (chat/log/diff) with minimal flicker and strong ergonomics.

## Summary of Delivered Work (context)

- Console hygiene
  - No alternate screen by default; explicit restore to main buffer on init
  - ClearOnInit by scrolling (preserves entire prior screen in scrollback)
  - Bracketed paste enable/disable; begin/end detection; app flag exposed
  - Console state restorer on shutdown and unhandled exceptions
- Options/API
  - `ApplicationOptions` (UseAlternateScreenBuffer, MouseTracking, RestoreConsoleOnExit, ClearOnInit, EnableBracketedPaste)
  - Init overload to pass options
- UX
  - Perf overlay (frame ms, output chars) + driver instrumentation
  - Virtualized log view (O(visible) rendering)

## Proposed Upgrades (remaining)

### 1) Buffered Flush Mode (optional)

- Add `ApplicationOptions.EnableFrameBufferFlush`.
- Drivers collect output into a frame buffer and write once per frame (or in larger chunks). Reduces flicker and VT overhead on heavy redraws.
- Implementation outline (NetDriver):
  - Accumulate StringBuilder writes into a per‑frame buffer when option is on.
  - Write once per row or per frame based on heuristics.
  - Keep current path as default to avoid regression risk.

### 2) Damage Tracking Tightening

- Reduce attribute churn and cursor moves in `UpdateScreen`:
  - Batch runs of the same attribute/foreground/background before emitting.
  - Avoid redundant `SetCursorPosition` calls (track last position).
  - Skip re‑clearing lines already fully redrawn.
- Add perf counters for attribute switches, cursor moves, bytes written.

### 3) Virtualized Controls

- VirtualListView<T>: selection, focus, owner‑draw callback; O(visible) rendering; large dataset friendly.
- VirtualDiffView: unified or side‑by‑side diff with virtualized hunks/rows; syntax/color hooks; sticky headers for file/hunk.
- VirtualTextView (future): virtualized wrapped text with incremental layout.

### 4) Command Palette & Split Panes

- Command palette: fuzzy search, register commands with labels and handlers, show key hints.
- Split panes: simple splitter control (horizontal/vertical); mouse + keyboard resizing; persisted layout.
- Status/log toggles: show/hide panes; minimal API for wiring runtime flags.

### 5) Styled Text & Links

- Introduce targeted Styled Spans for common views (labels/log/chat):
  - `Span/Line` with Color, Style (Bold/Italic/Underline), minimal nesting.
  - Optional OSC 8 hyperlinks for paths/URLs when terminal supports; graceful fallback.
- Keep retained‑mode; no global rework.

### 6) Input Model Polishing

- Keymap profiles: layered (default + app overrides); simple help view listing active bindings and chords.
- Bracketed paste chunk buffering: in addition to begin/end, buffer and deliver the paste as a single string to text inputs (opt‑in).
- IME/composition (future): minimal preedit/commit events (platform‑dependent).

### 7) Diagnostics & Testing

- Perf overlay enhancements: VT bytes, attribute changes, cursor moves; budget bar (e.g., 16 ms target).
- Headless renderer for snapshot tests: deterministic frame buffers for VirtualLogView/VirtualListView/VirtualDiffView.
- Bench kit: micro‑bench on write throughput and redraw of representative screens.

## API Changes (proposed)

- `ApplicationOptions` (existing + new):
  - `EnableFrameBufferFlush: bool` (default false)
  - (existing) UseAlternateScreenBuffer, MouseTracking, RestoreConsoleOnExit, ClearOnInit, EnableBracketedPaste
- `Application`: no breaking changes; continue using the Init overload with options.
- New controls:
  - `Terminal.Gui.Views.VirtualListView<T>`
  - `Terminal.Gui.Views.VirtualDiffView`
  - (delivered) `Terminal.Gui.Views.VirtualLogView`
- Diagnostics:
  - `Terminal.Gui.Diagnostics.PerfMetrics` (delivered)
  - `Terminal.Gui.Views.PerfOverlayView` (delivered)

## Migration / Compatibility

- All features are option‑gated or additive; retained‑mode continues to work as before.
- Apps opt into buffered flush and virtualized controls at their own pace.
- Default behavior remains conservative (no alt‑screen; scroll‑clear; mouse tracking off unless requested).

## Risks & Mitigations

- Terminal variance (tmux/screen/Windows):
  - Keep alt‑screen off by default; scroll‑clear; bracketed paste toggles; tmux‑aware defaults if needed.
- Buffered flush regressions:
  - Ship behind an option; perf overlay and counters to validate impact.
- Styled spans complexity:
  - Start narrow (labels/log/chat prefixes); defer full text engine.

## Test Strategy

- Unit tests for option‑gated VT sequences (no 1049h unless requested; bracketed paste toggles correctly; scroll‑clear emits expected sequences).
- Snapshot tests for virtualized views with a fake driver.
- Benchmarks on redraw heavy screens (list/log/diff) with/without buffered flush.

## Out of Scope (for now)

- Full declarative/virtual DOM layer (kept as a future opt‑in helper).
- Full IME/composition support (longer‑term work).

---

This proposal is intentionally parked while we pursue a Ratatui backend. It can be used as a reference if we return to Terminal.Gui work or want to upstream meaningful, focused changes.

