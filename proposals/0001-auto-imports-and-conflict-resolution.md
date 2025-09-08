Title: Auto‑Imports + Conflict Resolution (Multi‑Language)
Status: Draft
Owners: hoho core

Summary
Automate “add missing imports” and resolve symbol conflicts across languages with a calm, deterministic pipeline that runs after each patch. Prefer LSP/MCP code actions for accuracy and use an offline Roslyn fallback for C#. Resolve ambiguity via a preference map and stable global aliases to keep code clean without prompting.

Problem
- Patches often leave missing imports (CS0246/CS0103) or ambiguous symbols (CS0104), breaking builds and disrupting the zen workflow.
- Different languages/environments require different tools (Roslyn, tsserver, pyright, rust‑analyzer, gopls). We need one consistent “fix” story across the repo.

Goals
- Add missing imports automatically for C#/TS/JS/Python/Rust/Go.
- Resolve conflicts deterministically (preference map → alias → fully‑qualify).
- Run quietly after every patch (post‑apply hook) and on demand (`hoho fix`).
- Keep code readable with global aliases for recurring collisions.

Non‑Goals
- No intrusive prompts by default. Prompt only if ambiguity can’t be resolved by the preference map.
- No heavy UI — everything happens in the background, with concise logs.

Architecture
1) LSP/MCP Bridge (Preferred)
- Hoho invokes language servers via MCP and requests code actions per file:
  - C#: Roslyn LSP — Add Using, Fully Qualify, Add Alias
  - TS/JS: tsserver — addImport, organizeImports
  - Python: pyright for suggestions; ruff/isort to order imports
  - Rust: rust‑analyzer — add use, qualify; rustfmt
  - Go: gopls — add import; goimports
- Conflict policy:
  - If multiple candidates for a symbol: consult “hoho.imports.json” (symbol → preferred namespace).
  - If still ambiguous: auto‑alias with a stable alias (e.g., GuiWindow, JsonDoc) to avoid future conflicts.
- Integration points:
  - After `patch-apply`: run “fix-imports” via MCP
  - Manual: `hoho fix`

2) Roslyn‑based C# Fallback (Offline)
- Console tool “Hoho.FixImports” using MSBuildWorkspace:
  - Load solution, compile, gather diagnostics.
  - Handle:
    - CS0246/CS0103 (missing type/symbol): find symbol across references → add using if single hit; else preference → alias → fully‑qualify.
    - CS0104 (ambiguous): add alias (global) or fully‑qualify at usage sites.
  - Apply edits via DocumentEditor (Roslyn API), then run `dotnet format`.
- Hook into `hoho fix` as a fallback if no LSP available.

Preference Map + Aliasing
- hoho.imports.json at repo root, example:
```
{
  "View": "Terminal.Gui.ViewBase.View",
  "Window": "Terminal.Gui.Views.Window",
  "Key": "Terminal.Gui.Input.Key",
  "Rect": "System.Drawing.Rectangle"
}
```
- For recurring collisions (e.g., View, Window, Key): add global aliases in Hoho.App/GlobalUsings.cs:
```
global using GuiView = Terminal.Gui.ViewBase.View;
global using GuiWindow = Terminal.Gui.Views.Window;
global using GuiKey = Terminal.Gui.Input.Key;
```
- Use aliases in code (GuiView/GuiWindow/GuiKey) to keep sources stable and readable.

Pipeline
1) Post‑patch hook (already wired): `hoho patch-apply` → `hoho fix`
2) `hoho fix` runs:
  - Ensure GlobalUsings.cs (App/Core) for baseline imports
  - Try LSP/MCP code actions for imports/alias/qualify
  - Fallback to Roslyn “Hoho.FixImports” for C#
  - Run formatters: `dotnet format`, `prettier/eslint --fix`, `ruff --fix/isort`, `rustfmt`, `goimports`

CLI & Config
- Command: `hoho fix` (already present; will be extended to run LSP/MCP and language formatters)
- Pref file: `hoho.imports.json` (optional; used to break ties)
- Global aliases: managed in `Hoho.App/GlobalUsings.cs` and `Hoho.Core/GlobalUsings.cs`

Milestones
- M0: C# baseline (GlobalUsings + dotnet format) [SHIPPED]
- M1: Roslyn “Hoho.FixImports” (offline) + integrate into `hoho fix`
- M2: MCP LSP endpoints for Roslyn/tsserver/pyright/rust‑analyzer/gopls
- M3: Cross‑language fix‑imports pipeline (priority C# → TS/JS → Python → Rust/Go)
- M4: Preference map + alias generator (auto‑suggest recurring aliases)

Risks / Mitigations
- Ambiguity across many libraries → use preference map + aliases; prompt only as a last resort.
- LSP availability → maintain Roslyn offline fallback.
- Performance → scope to changed files; cache symbol indices per solution build.

Paths / Ownership
- C#: `Hoho.App/GlobalUsings.cs`, `Hoho.Core/GlobalUsings.cs`
- Fix entry: `Hoho.App/Program.cs` → `RunFixAsync`
- Proposal owners: hoho core

