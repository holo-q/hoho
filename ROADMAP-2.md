> Yes — a few more needles worth sweeping before we call 1‑to‑1 parity locked. Here’s a tight checklist mapped to
  codex-rs modules, with what to verify and port (parity only; no extras).

  High‑value sweeps

  - Composer history (bottom_pane/chat_composer_history.rs)
      - Only treat Up/Down as history when cursor is at column 0 and text equals last recalled entry.
      - Add cross‑session persistent history (append‑only), merged with local entries; avoid duplicates.
      - Async fetch in Codex is internal; we can read from file synchronously.
  - Paste handling (clipboard_paste.rs, bottom_pane/paste_burst.rs)
      - Keep bracketed‑paste smoothing: tighten our burst thresholds and suppress all popups (including “@” and
  backtrack) during a burst.
  - Status indicator (status_indicator_widget.rs)
      - Show queued user prompts as dim sublines (cap 2–3; ellipsis if more). We already show “• queued N”; add
  sublines for parity.
      - Keep shimmer + elapsed + “Esc to interrupt”; no spinner.

  Rendering + formatting

  - Markdown/light formatting (markdown.rs, text_formatting.rs, wrapping.rs)
      - Ensure we render assistant text calmly:
      - Respect soft wraps; avoid double-wrapping artifacts.
      - Apply minimal formatting (bold headers, code blocks) to match Codex’s calm style (no color spam).
  - Wrapping consistency (live_wrap.rs, render/line_utils.rs)
      - Verify multi-line wrapping aligns with Codex snapshots (continuation indent, prefix handling “You:”/“Codex:”).
  - History insertion vs. live view (insert_history.rs)
      - Codex prevents live-only lines from leaking into history (vt100_streaming_no_dup tests). Our non-stream render
  is already calm, but verify we never insert uncommitted lines into history.

  Tooling flows (keep parity off unless visible)

  - Diff rendering & pager (diff_render.rs, pager_overlay.rs)
      - Present in codex-rs; if not visible in your build, keep this off. We already use CLI git diff; do not expose
  TUI pager yet.
  - Slash commands / overlays (slash_command.rs, help/onboarding)
      - Skip; not in your release UX. Ensure composer ignores “/” as a special trigger unless ExperimentalUi is on
  (it’s off).

  Approvals + sandbox (quiet, strict)

  - user_approval_widget.rs
      - Keep TUI approvals modal gated off (ExperimentalUi). CLI gating is already in parity.
  - Shell sandbox
      - We enforce AllowedRoot + no-network; ensure error messages are calm and non-verbose, only surfaced when
  violated.

  Apply-patch (final touches)

  - parser.rs parity on hunk windows
      - Bound resync window; normalize CRLF; keep errors short and informative.
      - Print “no changes” summary when applicable; avoid noisy success lines.

  Key hints

  - key_hint.rs (phrasing)
      - Align info-line phrasing and spacing exactly. If you share a snapshot, I’ll mirror wording and margins.

  Session logging

  - session_log.rs, updates.rs
      - Ensure transcript writes are JSONL and append-only; we already do this. Confirm no live-only content is
  written.

  What I’ll implement next (parity-only)

  - Composer Up/Down guard + persistent history file (merged with local; no dupes).
  - Status sublines for queued items (dim, capped).
  - Paste-burst tightening + full popup suppression during paste.
  - Apply-patch summaries: no-op summary; bounded resync window; calm error phrasing.
  - Info-line phrasing alignment (pending a screenshot/text).

  If you want, send a brief screenshot/text of your Codex info line and a short assistant message sample; I’ll match
  spacing and exact phrasing before moving on.

▌Render down a document with every observation, and include path references and tags to help you recover context later

> Codex Parity Observations

  - [TUI][Layout]
      - Single-column chat list with role prefixes; calm, non-streaming assistant render.
      - Status line shows “Working (elapsed • Esc to interrupt)”; no spinner.
      - Composer sits at the bottom; info line beneath shows concise hints.
      - Paths: codex-rs/tui/src/chatwidget/ • Hoho.App/Tui/ChatView.cs • Hoho.App/Tui/TuiApp.cs
      - Paths: codex-rs/tui/src/chatwidget/ • Hoho.App/Tui/ChatView.cs • Hoho.App/Tui/TuiApp.cs
  -
  [Status][Queued]
      - Status indicator shimmer + elapsed; “Esc to interrupt”.
      - Queued submissions during a running turn; Codex shows quiet status and can show queued lines beneath.
      - Current: Hoho shows “• queued N”; next parity step: dim sublines for first 2–3 queued user prompts with
  ellipsis.
      - Paths: codex-rs/tui/src/status_indicator_widget.rs • Hoho.App/Tui/StatusIndicatorView.cs
  -
  [Composer][History]
      - History navigation rules: Up/Down only when caret at column 0 AND the text equals last recalled entry; merges
  persistent history with local session history; avoids dupes.
      - Current: Hoho implements Alt+Up/Down (local session). Next: add Up/Down guard + cross-session history file.
      - Paths: codex-rs/tui/src/bottom_pane/chat_composer_history.rs • Hoho.App/Tui/TuiApp.cs (history list)
  -
  [Composer][Backtrack]
      - Esc once primes; Esc again shows preview of last user message; Enter accepts; Esc cancels.
      - Current: Implemented in Hoho with overlay semantics.
      - Paths: codex-rs/tui/src/app_backtrack.rs, chatwidget/interrupts.rs • Hoho.App/Tui/BacktrackPreview.cs •
  Hoho.App/Tui/TuiApp.cs
  -
  [Composer][Paste]
      - Codex uses bracketed-paste smoothing; suppresses popups and extraneous redraws during bursts.
      - Current: Hoho has paste-burst heuristic; blocks “@” popup. Next: tighten thresholds and suppress all popups
  during bursts (e.g., backtrack/overlays).
      - Paths: codex-rs/tui/src/clipboard_paste.rs, bottom_pane/paste_burst.rs • Hoho.App/Tui/ComposerView.cs •
  Hoho.App/Tui/TuiApp.cs
  -
  [FileSearch][Gating]
      - File search (“@”) and popups exist in codex-rs; may be hidden in your released build.
      - Current: Hoho gates Ctrl+K and “@” search under ExperimentalUi=false (off by default).
      - Paths: codex-rs/tui/src/file_search.rs, bottom_pane/file_search_popup.rs • Hoho.App/Tui/FileSearchPopup.cs •
  Hoho.App/Tui/TuiApp.cs
  -
  [ApplyPatch][Resilience]
      - Codex tolerant to CRLF/LF and small context shifts; produces concise per-file summary; errors are short and
  clear.
      - Current: Hoho normalizes CRLF, resyncs forward within a window, returns per-file (+/-) summary; CLI prints
  the summary.
      - Next: bound resync window; add “no changes” summary; tighten error phrasing to Codex tone.
      - Paths: codex-rs/core/src/tool_apply_patch.rs, apply-patch/src/parser.rs • Hoho.Core/Tools/PatchService.cs
  -
  [Approvals][Sandbox]
      - Approvals modal appears only when policy demands; sandbox enforced quietly.
      - Current: CLI --ask-for-approval and --sandbox enforced (patch-apply, repo-commit). TUI dialogs gated behind
  ExperimentalUi.
      - Paths: codex-rs/tui/src/user_approval_widget.rs, bottom_pane/approval_modal_view.rs • Hoho.Core/Approvals/
  ApprovalService.cs • Hoho.App/Tui/ApprovalModal.cs
  -
  [Shell][Sandbox]
      - Allowed root; block network commands unless allowed; allow local git status/diff/blame.
      - Current: Hoho’s ShellRunner blocks networky commands (curl/wget/pip/npm/pnpm/yarn/apt/brew; git pull/fetch) and
  enforces AllowedRoot.
      - Paths: codex-rs/core/src/seatbelt.rs, core/src/shell.rs • Hoho.Core/Abstractions/IShellRunner.cs • Hoho.Core/
  Services/ShellRunner.cs • Hoho.Core/Repo/GitService.cs
  -
  [Resume][Continue]
      - Not in your current Codex build. Hoho gates CLI/TUI resume under ExperimentalUi=false (off), preserving parity.
      - Paths: codex-rs/tui/src/resume_picker.rs • Hoho.App/Program.cs (gating) • Hoho.App/Tui/ResumePicker.cs (gated)
  -
  [Rendering][Markdown]
      - Codex applies calm formatting: minimal bold/headers, controlled wrapping, no color spam.
      - Current: Hoho renders plain text with prefix coloring; next: optional markdown-lite (bold headers/code blocks)
  with calm styling (only if confirmed).
      - Paths: codex-rs/tui/src/markdown.rs, render/markdown_utils.rs, text_formatting.rs, wrapping.rs • Hoho.App/
  Tui/ChatView.cs
  -
  [History vs Live][Insertion]
      - Codex ensures uncommitted “live” content never enters history (vt100_streaming_no_dup tests).
      - Current: Hoho non-streams to history; safe by design. Keep it.
  -
  [Key Hints][Phrasing]
      - Align wording/spacing with Codex (cyan tips). Need snapshot to match exactly.
      - Current: Hoho info line shows parity hints without experimental shortcuts by default.
      - Paths: codex-rs/tui/src/key_hint.rs, styles.md • Hoho.App/Tui/TuiApp.cs

  Hoho Implementation Map

  - [TUI]
      - Hoho.App/Tui/TuiApp.cs — orchestrates chat loop, status, composer, key handling, queue draining.
      - Hoho.App/Tui/ChatView.cs — single-column chat render; colored prefixes; wrapping.
      - Hoho.App/Tui/StatusIndicatorView.cs — shimmer header; elapsed timer; interrupt; queued count.
      - Hoho.App/Tui/ComposerView.cs — cyan left gutter; paste-burst detection; placeholder; inner TextView.
      - Hoho.App/Tui/BacktrackPreview.cs — overlay for Esc–Esc edit preview.
      - Hoho.App/Tui/FileSearchPopup.cs — gated (ExperimentalUi).
      - Hoho.App/Tui/ApplyPatchDialog.cs, CommitDialog.cs — gated (ExperimentalUi).
      - Hoho.App/Tui/ResumePicker.cs — gated (ExperimentalUi).
      - Hoho.App/Tui/ResumePicker.cs — gated (ExperimentalUi).
  -
  [Core]
      - Hoho.Core/Agents/AgentRunner.cs — run once; calm stream buffer; system prompt; cancel support.
      - Hoho.Core/Sessions/TranscriptStore.cs — JSONL append/read; session roots.
      - Hoho.Core/Sessions/SessionDiscovery.cs — list sessions; preview first user message.
      - [TODO parity] persistent history file & merge (new module).
      - Hoho.Core/Tools/PatchService.cs — CRLF normalization, forward resync, (+/-) summary.
      - Hoho.Core/Tools/FileService.cs — workspace root resolution; sandbox read-only enforcement.
      - Hoho.Core/Services/ShellRunner.cs — AllowedRoot + no-network guard; stream chunks.
      - Hoho.Core/Repo/GitService.cs — status/diff/blame, commit; passes sandbox options.
      - Hoho.Core/Approvals/ApprovalService.cs — policy gating; CLI prompts.
      - Hoho.Core/Providers/OpenAIProvider.cs — SSE + fallback; calm buffering.
      - Hoho.Core/Configuration/HohoConfig — ExperimentalUi (default false), auth, sandbox defaults.

  Parity Gaps To Close Next

  - [History][Persistent]
      - Add cross-session history file: append-only; de-dup consecutive; merge with in-memory session history.
      - Up/Down guard: Only navigate when caret==0 and text==last recalled history; otherwise ignore to avoid hijacking
  edits.
      - Tag: [TODO][HistoryPersist] • Paths: Hoho.App/Tui/TuiApp.cs (navigation guard), new Hoho.Core/Sessions/
  HistoryStore.cs
      - Tag: [TODO][HistoryPersist] • Paths: Hoho.App/Tui/TuiApp.cs (navigation guard), new Hoho.Core/Sessions/
  HistoryStore.cs
  -
  [Status][Queued Sublines]
      - Render first 2–3 queued user prompts under status as dim lines; add ellipsis if more.
      - Tag: [TODO][StatusQueueLines] • Paths: Hoho.App/Tui/StatusIndicatorView.cs (queued lines render)
  -
  [Paste][Smoothing]
      - Tighten paste-burst thresholds; suppress all popups (backtrack, file search) during bursts; only commit text
  to composer.
      - Tag: [TODO][PasteBurstGuard] • Paths: Hoho.App/Tui/ComposerView.cs, Hoho.App/Tui/TuiApp.cs
  -
  [ApplyPatch][Tone+Bounds]
      - Bound forward resync window; on mismatch show calm message: “context mismatch near , hunk skipped” (or fail
  fast based on mode).
      - Include “no changes detected” summary for no-ops.
      - Tag: [TODO][PatchTone][PatchBounds] • Paths: Hoho.Core/Tools/PatchService.cs
  -
  [Hints][Phrasing]
      - Match info-line phrasing/margins exactly to Codex; requires reference screenshot or text capture.
      - Tag: [TODO][HintsParity] • Paths: Hoho.App/Tui/TuiApp.cs

  Quick Test Plan (Manual)

  - [Non-Streaming][Calm]
      - Submit prompt; ensure assistant message appears after completion (no token spam).
  - [Interrupt][Esc]
      - During a run, press Esc; status stops; “🖐  Turn interrupted” appears.
  - [Queue][Draining]
      - While running, Enter with another prompt; status shows “• queued 1”; after completion, second message runs.
  - [Backtrack][EscEsc]
      - Ensure overlay appears only when composer empty; Enter accepts; Esc cancels.
  - [History][Guard]
      - Up/Down should only navigate when caret==0 and text==last recalled; otherwise ignore.
  - [Patch][Summary]
      - Apply patch add/update/delete; confirm per-file summary; read-only sandbox blocks writes; CRLF/misaligned
  contexts handled gracefully.

  Tags Index

  - [TUI][Layout][Status][Composer][History][Backtrack][Paste][Queued]
  - [FileSearch][Gating]
  - [ApplyPatch][Resilience][Tone][Bounds][Summary]
  - [Approvals][Sandbox][Shell]
  - [Resume][Continue][Gating]
  - [Rendering][Markdown][Wrapping]
  - [Parity][ExperimentalUi][Next][TODO]

  If you can share a current Codex screenshot (status + info line + a short assistant render), I’ll close the remaining
  phrasing/spacing gaps exactly and proceed to implement the persistent history + queued sublines rendering.
