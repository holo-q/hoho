# Hoho

[![CI](https://github.com/holo-q/hoho/actions/workflows/ci.yml/badge.svg)](https://github.com/holo-q/hoho/actions/workflows/ci.yml)

Hoho is a calm, non‑streaming CLI/TUI coding agent written in C#. It aims for 1:1 parity with the OpenAI Codex CLI UX: single‑column chat, ESC interrupt/backtrack, keyboard history, and a safe `apply_patch` flow. Non‑parity experiments exist but stay gated behind an Experimental UI flag.

## Features

- Calm TUI: single column chat, no streaming; ESC to interrupt.
- History: Up/Down recall; queued sends while a run is active.
- Providers: `echo` (local) and `openai` (via `OPENAI_API_KEY`).
- Safe patching: `patch-apply` with per‑file summary and post‑patch `fix`.
- Sandbox/approvals: constrain writes and gate destructive actions.
- Cross‑platform: .NET 9 on Linux, macOS, and Windows.

## Requirements

- .NET SDK 9.0+
- Optional for OpenAI: environment variable `OPENAI_API_KEY` (or set in config below).

## Build

- Restore and build: `dotnet build Hoho.sln -c Release`
- Run the TUI: `dotnet run --project Hoho.App`
- Publish (native AOT is configured): `dotnet publish Hoho.App -c Release -r linux-x64` (or `osx-x64`, `win-x64`).

## Quick Start

- TUI (default provider `echo`):
  - `dotnet run --project Hoho.App`
- One‑shot chat (non‑interactive):
  - `dotnet run --project Hoho.App -- chat --provider echo "List files in the repo"`
  - OpenAI example: `OPENAI_API_KEY=... dotnet run --project Hoho.App -- chat --provider openai -m gpt-4o-mini "Say hi calmly"`

## Providers

- `echo`: local echo provider (no network).
- `openai`: uses `OPENAI_API_KEY` from environment, or from `~/.hoho/config.json` under `Secrets`.

Example config (`~/.hoho/config.json`):

```
{
  "Sandbox": {},
  "Profile": "default",
  "Secrets": { "OPENAI_API_KEY": "sk-..." },
  "AuthProvider": "none",
  "ChatGptSession": null,
  "ExperimentalUi": false
}
```

## Sandbox & Approvals

- Sandbox: `--sandbox read-only|workspace-write|danger-full-access`
- Approvals: `--ask-for-approval untrusted|on-failure|on-request|never`

These flags exist on commands like `patch-apply`, and in automation (`exec`).

## Apply Patches & Fixers

- Apply a patch from stdin: `cat patch.txt | dotnet run --project Hoho.App -- patch-apply --sandbox workspace-write --ask-for-approval on-request`
- Run fixers (organize imports / format): `dotnet run --project Hoho.App -- fix -C .`

`patch-apply` prints a compact per‑file summary and runs `fix` automatically afterward when applicable.

## Data Locations

- Transcripts: `~/.hoho/sessions/<session-id>/transcript.jsonl`
- Config: `~/.hoho/config.json`

## CI

GitHub Actions builds the solution on push/PR for Linux, macOS, and Windows. See the badge above or the Actions tab for status.

## Contributing

- See `proposals/` for planned enhancements. Non‑parity UI stays behind the Experimental UI flag by default.
- PRs and issue reports welcome.

## Project Vision

- For the extended vision and brand doc, see `docs/README.md`.
