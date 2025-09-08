# Hoho Port Plan (Codex → C# / Terminal.Gui)

Goal: Faithful, end-to-end port of Codex CLI to a C# codebase named Hoho, using Terminal.Gui for the TUI. Maintain feature parity, but adapt UX idioms to native TUI patterns.

## Principles
- Parity-first: replicate core functionality and workflows before enhancements.
- Clear boundaries: split `Hoho.Core` (domain + services) and `Hoho.App` (TUI + CLI host).
- Offline-friendly: avoid hidden network coupling; all integrations are explicit.
- Testable seams: abstract shell, filesystem, config, and model backends.

## High-Level Architecture
- Hoho.Core
  - Configuration: profiles, auth, providers (env, files).
  - Session + Transcript: messages, tool calls, artifacts.
  - Tools: shell runner, file ops, planner API, task orchestration.
  - Agents: chat logic, command routing, streaming transcript sinks.
  - Integrations: model backends (ChatGPT-native auth first), git status reader, formatter runners.
- Hoho.App (Terminal.Gui)
  - Main: layout manager, windows/panes, status bar, keybindings.
  - Views: chat, files, diffs, plan/steps, logs, settings.
  - Controllers: glue between UI events and Hoho.Core services.
- Hoho.Decomp (not used now): retained for future challenges.

## Feature Parity Map (Initial)
1) Chat & transcript
   - Messages with roles, streaming output, copy/save.
   - System/prompt presets, quick-reply macros.
2) Tools / actions
   - Shell execution (sandbox-aware), apply_patch, file ops.
   - Plan steps (create/update), progress rendering.
3) Workspace awareness
   - Repo tree, file open/search, blame, diff preview, staging assist.
4) Runs & tasks
   - Long-running “runs”, live logs, cancel, retry.
5) Config
   - Profiles, API provider selection, local overrides, secrets loading.
6) Extensibility
   - Tool interface, simple plugin registry.

## Milestones
M0 – Foundations (this PR)
- Docs: PORT plan + architecture + UI design + component map + phases.
- Skeleton projects: `Hoho.Core` (classlib), ensure `Hoho.App` boots Terminal.Gui.
- Solution wiring.

M1 – Core domain & shell
- Sessions, transcript, message model, persistence (JSONL).
- ShellRunner abstraction w/ sandbox flags, streaming stdout.
- FileService: read/write, patch application, safe guards.

M2 – TUI basics
- Chat view with input, output pane, scrollback.
- Status bar, log pane, global shortcuts.

M3 – Tools & planner
- Plan store (steps/status), UI to create/update steps.
- ApplyPatch tool pipeline and diff preview.

M4 – Repo & diffs
- Repo browser, open file, search-in-repo, minimal blame.
- Diff view for pending changes; stage/commit integration.

M5 – Providers & settings
- Config UI, profile switcher, provider polymorphism (mock + local LLM if any).

M6 – Polishing & parity closure
- Trace logging, diagnostics, UX papercuts, docs.

## Risks & Mitigations
- TUI complexity: keep views modular; prefer simple panes and explicit commands.
- Streaming: abstract sinks; batch updates for smooth rendering.
- Cross-platform: stick to .NET APIs and Terminal.Gui patterns.

## Deliverables in This Pass
- This PORT.md
- docs/port: ARCHITECTURE, UI-DESIGN, COMPONENT-MAP, PHASES
- Hoho.Core csproj scaffold (no heavy code yet)
