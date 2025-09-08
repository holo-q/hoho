# Hoho Architecture (Port from Codex)

This document maps Codex CLI's conceptual components to a C# design split into Hoho.Core (domain/services) and Hoho.App (Terminal.Gui TUI host).

## Components
- Config: Profiles, auth, provider selection, sandbox presets -> `Hoho.Core.Configuration`.
- Session: Transcript, messages, attachments, tool calls -> `Hoho.Core.Sessions`.
- Tools: Shell exec, apply_patch, file ops -> `Hoho.Core.Tools`.
- Planner: Plan steps CRUD and status -> `Hoho.Core.Planning`.
- Approvals/Sandbox: Policy + runner wiring -> `Hoho.Core.Sandbox`.
- Git/Repo: Status, diff, blame (thin) -> `Hoho.Core.Repo`.
- Logging/Tracing: Structured logs, events -> `Hoho.Core.Diagnostics`.
- Providers: Model backends (ChatGPT native auth first-class) -> `Hoho.Core.Providers`.

## Process Model
- Interactive session loop: UI events -> Controllers -> Core services.
- Long-running commands stream events to Transcript + Log sink.
- Approval path controls ShellRunner (read-only vs workspace-write vs full-access).

## Storage
- Config: `~/.hoho/config.toml` (similar shape to Codex).
- Sessions: JSONL transcript in `.hoho/sessions/<id>/transcript.jsonl`.
- Cache: adhere to sandbox policy; persistent cache under `~/.hoho/cache`.

