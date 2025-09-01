# HOHO - Claude Code Decompilation Tool

## Project Documentation Architecture

### Core Documentation Files
→ @docs/README.md (Project overview and getting started guide)
→ @docs/ROADMAP.md (Development roadmap and future plans)
→ @docs/ARCHITECTURE_BLUEPRINT.md (System architecture and design principles)

### Claude Code Analysis Documentation
→ @docs/CLAUDE_CODE_ANALYSIS.md (Analysis of Claude Code internals)
→ @docs/CLAUDE_CODE_DEEP_DIVE_LEARNINGS.md (Deep technical insights from Claude Code)
→ @docs/CLAUDE_CODE_TO_HOHO_PORT_STRATEGY.md (Strategy for porting Claude Code features)

### Decompilation System Documentation
→ @docs/AUTOMATED_DECOMPILATION_WORKFLOW.md (Automated decompilation process)
→ @docs/DECOMPILATION_STATUS_REPORT.md (Current decompilation system status)
→ @docs/LSP_DECOMPILATION_IMPLEMENTATION.md (LSP-based decompilation implementation)
→ @docs/MODULE_EXTRACTION_ANALYSIS.md (Module extraction analysis and findings)

### Symbol and Structural Analysis Documentation
→ @docs/STRUCTURAL_MATCHING_USAGE_TREE.md (Structural matching usage patterns)
→ @docs/SYMBOL_MATCHING_CONSTRAINTS.md (Symbol matching constraints and rules)
→ @docs/SYMBOL_RENAMING_STRATEGY.md (Symbol renaming strategy and implementation)

### Performance and Library Documentation
→ @docs/HIGH_PERFORMANCE_LIBRARIES.md (High-performance library recommendations)
→ @docs/LIBRARY_RECOMMENDATIONS.md (General library recommendations)
→ @docs/TERMINAL_GUI_PERFORMANCE_INVESTIGATION.md (Terminal GUI performance analysis)
→ @docs/TUI_EVALUATION.md (Terminal User Interface evaluation)

## Project Context

HOHO is a decompilation tool that reverses Claude Code binaries back to readable source code, enabling analysis and understanding of complex CLI applications. The project focuses on:

- **LSP-based decompilation** for structured code analysis
- **Symbol matching and renaming** for code clarity
- **High-performance libraries** for efficient processing
- **Terminal GUI integration** for user-friendly interfaces
- **Automated workflows** for streamlined decompilation

## Core Directives

- Always use UV for Python operations: `uv run`, `uv add`, etc.
- Prefer fd and rg over find and grep: `fd` for file finding, `rg` for text search
- Update existing code rather than creating separate files
- Use modern command utilities: eza instead of ls, ouch instead of tar

## Development Workflow

The project maintains comprehensive documentation for all aspects of the decompilation process, from initial analysis through final symbol renaming and performance optimization.
