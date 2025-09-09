Title: hoho fix — Post‑Patch Calm Cleanup Pipeline
Status: Draft
Owners: hoho core

Problem
- Patches may leave imports, formatting, and small consistency issues that distract from a zen UX.

Goals
- One calm command that tidies the repo after changes.
- Safe by default, no jitter, no prompts.

Current
- hoho fix creates GlobalUsings.cs (App/Core) and runs dotnet format (if available).

Proposed Pipeline (per language)
- C#: dotnet format; Roslyn FixImports (add using/alias/qualify via LSP/MCP or offline tool)
- TS/JS: prettier/eslint --fix; tsserver organizeImports
- Python: ruff --fix; isort
- Rust: rustfmt
- Go: goimports

Invocation Points
- After hoho patch-apply (already wired)
- Manual: hoho fix (-C workdir)
- CI: optional pre-commit hook

Extensibility
- Prefer LSP/MCP code actions where possible (uniform across languages).
- Fallback to formatters/organizers where LSP is unavailable.

Non‑Goals
- No interactive conflict resolution in the default path; rely on hoho.imports.json + aliases.

