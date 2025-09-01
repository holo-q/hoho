"""
Main CLI interface for hoho productivity agent
"""

import typer
from rich.console import Console
from pathlib import Path

from .glb import DECOMP_DIR, CLI_AGENTS
from .ascii_art import get_ok_face

console = Console()
app = typer.Typer()

@app.command()
def version():
    """Show hoho version"""
    from hoho import __version__
    console.print(f"hoho v{__version__}")

@app.command()
def home():
    """Show hoho home screen with Saitama face"""
    console.print(get_ok_face("saitama"))
    console.print("\n[bold red]HOHO[/bold red] - The CLI Agent That Just Says 'OK.'")
    console.print("[dim]Shadow Protocol Active[/dim]")

@app.command()
def status():
    """Show project status and decompilation data"""
    if not DECOMP_DIR.exists():
        console.print("[yellow]No decomp data found. Run 'hoho-decompile decompile-all' first.[/yellow]")
        return
    
    for agent_name, config in CLI_AGENTS.items():
        agent_dir = config["output_dir"]
        if agent_dir.exists():
            console.print(f"[green]✓[/green] {agent_name}")
            analysis_dir = agent_dir / "analysis"
            if analysis_dir.exists():
                files = list(analysis_dir.glob("*"))
                console.print(f"  └─ {len(files)} analysis files")
        else:
            console.print(f"[red]✗[/red] {agent_name} (not found)")

def main():
    """Entry point for hoho command"""
    app()

if __name__ == "__main__":
    main()