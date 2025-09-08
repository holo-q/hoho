# Component Map (Codex -> Hoho)

| Codex Concept | Hoho Namespace | Notes |
| --- | --- | --- |
| Sandbox & Approvals | Hoho.Core.Sandbox | Modes: read-only, workspace-write, full-access; policies on-request/on-failure/never |
| Shell Exec | Hoho.Core.Tools.ShellRunner | Landlock/Seatbelt-equivalent flags; streaming stdout/stderr |
| Apply Patch | Hoho.Core.Tools.PatchService | Same envelope format; safety checks |
| Plan (update_plan) | Hoho.Core.Planning.PlanService | Steps: pending/in_progress/completed; one active |
| Transcript | Hoho.Core.Sessions | JSONL persistence; attachments metadata |
| Config | Hoho.Core.Configuration | TOML-based; profiles; provider selection |
| Providers | Hoho.Core.Providers | ChatGPT native auth primary; API key optional |
| Repo | Hoho.Core.Repo | git status/diff/blame (thin) |
| TUI | Hoho.App.Views.* | Terminal.Gui panes + controllers |

