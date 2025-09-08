<!-- Fancy centered prelude -->
<div align="center">
  <img src="docs/holoq-hoho-wide.png" alt="Hoho Banner" width="100%" />
  <br/>
  <p><strong>THE CLI AGENT THAT JUST SAYS "OK."</strong></p>
  <p>CLASSIFICATION: UNCLASSIFIED · CODENAME: SAITAMA PROTOCOL · STATUS: ACTIVE · CLEARANCE: SHADOW</p>
  <p>— Overwhelming power through calm, principled simplicity —</p>
  <p>
    <a href="https://github.com/holo-q/hoho/actions/workflows/ci.yml">
      <img alt="CI" src="https://github.com/holo-q/hoho/actions/workflows/ci.yml/badge.svg" />
    </a>
  </p>
</div>

---

## Table of Contents

- [Overview](#overview)
- [Codex Parity](#codex-parity)
- [Requirements](#requirements)
- [Build & Run](#build--run)
- [Quick Start](#quick-start)
- [Key Commands](#key-commands)
- [Providers](#providers)
- [Config](#config)
- [Sandbox & Approvals](#sandbox--approvals)
- [Apply Patches & Fixers](#apply-patches--fixers)
- [Data Locations](#data-locations)
- [Implementation Notes](#implementation-notes)
- [Contributing](#contributing)
- [Extended Vision](#extended-vision)

## Overview

- Hoho is a calm, non‑streaming CLI/TUI coding agent written in C# (.NET 9).
- It targets strict 1:1 UX parity with the OpenAI Codex CLI: single‑column chat, Esc interrupt/backtrack, keyboard history, safe `apply_patch`.
- Non‑parity ideas live behind an Experimental UI flag and in `proposals/`.

## Codex Parity

- Single‑column chat, calm writes (no token streaming)
- Esc to interrupt; Esc Esc to preview backtrack
- Up/Down history recall with guard when composer not empty
- Queue sends during a running turn
- Safe `patch-apply` with per‑file summary and no‑op detection

## Requirements

- .NET SDK 9.0+
- Optional: `OPENAI_API_KEY` for the OpenAI provider

## Build & Run

- Restore/build: `dotnet build Hoho.sln -c Release`
- TUI (default `echo` provider): `dotnet run --project Hoho.App`
- Native AOT publish: `dotnet publish Hoho.App -c Release -r linux-x64` (or `osx-x64`, `win-x64`)

## Quick Start

- One‑shot chat: `dotnet run --project Hoho.App -- chat --provider echo "List files"`
- OpenAI example: `OPENAI_API_KEY=... dotnet run --project Hoho.App -- chat --provider openai -m gpt-4o-mini "Say hi calmly"`
- Automation (Codex‑style): `dotnet run --project Hoho.App -- exec --provider openai -m gpt-4o-mini --sandbox workspace-write --ask-for-approval on-failure "Add a README section"`

## Key Commands

- `chat` — send a single prompt to the provider
- `exec` — automation mode with sandbox/approvals
- `patch-apply` — apply an apply_patch envelope from stdin or file
- `fix` — organize imports and run `dotnet format`
- `repo-status|repo-diff|repo-commit` — helpful git wrappers

## Providers

- `echo` — local echo, no network
- `openai` — requires `OPENAI_API_KEY` (env or `~/.hoho/config.json` under `Secrets`)

## Config

- Path: `~/.hoho/config.json`
- Example:
  {
    "Sandbox": {},
    "Profile": "default",
    "Secrets": { "OPENAI_API_KEY": "sk-..." },
    "AuthProvider": "none",
    "ChatGptSession": null,
    "ExperimentalUi": false
  }

## Sandbox & Approvals

- `--sandbox` read-only | workspace-write | danger-full-access
- `--ask-for-approval` untrusted | on-failure | on-request | never
- Used on `patch-apply`, `repo-commit`, and in `exec` mode

## Apply Patches & Fixers

- From stdin: `cat patch.txt | dotnet run --project Hoho.App -- patch-apply --sandbox workspace-write --ask-for-approval on-request`
- After a successful patch, Hoho runs `fix` to organize imports and format

## Data Locations

- Transcripts: `~/.hoho/sessions/<session-id>/transcript.jsonl`
- Config: `~/.hoho/config.json`

## Implementation Notes

- Uses vendored Terminal.Gui v2 for the TUI (single‑column chat UI)
- Core/CLI separation: `Hoho.Core` (domain/services), `Hoho.App` (CLI/TUI)

## Contributing

- See `proposals/` for planned enhancements. Experimental UI remains off by default to preserve Codex parity.
- PRs and issue reports welcome.

## Extended Vision

- For the narrative/brand doc, see `docs/README.md`.
