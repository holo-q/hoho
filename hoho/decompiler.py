"""
Automated decompilation system for frontier lab CLI agents
"""

import asyncio
import subprocess
import shutil
from pathlib import Path
from typing import Dict, List, Optional
import httpx
import typer
from rich.console import Console
from rich.progress import track

from .glb import CLI_AGENTS, DECOMPILER_TOOLS, ARCHIVE_EXTENSIONS, ensure_directories

console = Console()
app = typer.Typer()

class CliDecompiler:
    def __init__(self):
        ensure_directories()
        
    async def fetch_github_releases(self, repo: str) -> Dict:
        """Fetch latest releases from GitHub API"""
        async with httpx.AsyncClient() as client:
            response = await client.get(f"https://api.github.com/repos/{repo}/releases/latest")
            return response.json()
    
    async def download_binary(self, url: str, output_path: Path) -> bool:
        """Download binary from URL"""
        try:
            async with httpx.AsyncClient() as client:
                response = await client.get(url, follow_redirects=True)
                output_path.write_bytes(response.content)
                return True
        except Exception as e:
            console.print(f"[red]Error downloading {url}: {e}[/red]")
            return False
    
    def extract_with_unpack_tools(self, archive_path: Path, extract_to: Path) -> bool:
        """Extract archives using various tools"""
        try:
            if shutil.which("ouch"):
                subprocess.run(["ouch", "decompress", str(archive_path), "-d", str(extract_to)], check=True)
            elif archive_path.suffix == ".tar.gz":
                subprocess.run(["tar", "-xzf", str(archive_path), "-C", str(extract_to)], check=True)
            elif archive_path.suffix == ".zip":
                subprocess.run(["unzip", str(archive_path), "-d", str(extract_to)], check=True)
            else:
                return False
            return True
        except subprocess.CalledProcessError as e:
            console.print(f"[red]Extraction failed: {e}[/red]")
            return False
    
    def decompile_binary(self, binary_path: Path, output_dir: Path) -> bool:
        """Attempt to decompile/analyze binary"""
        output_dir.mkdir(exist_ok=True, parents=True)
        
        # Try different decompilation approaches
        decompilers = [[*tool, str(binary_path)] for tool in DECOMPILER_TOOLS]
        
        for i, cmd in enumerate(decompilers):
            if not shutil.which(cmd[0]):
                continue
                
            try:
                result = subprocess.run(cmd, capture_output=True, text=True, check=True)
                output_file = output_dir / f"{cmd[0]}_output.txt"
                output_file.write_text(result.stdout)
                console.print(f"[green]✓[/green] {cmd[0]} analysis saved to {output_file}")
            except subprocess.CalledProcessError:
                console.print(f"[yellow]⚠[/yellow] {cmd[0]} analysis failed")
                
        return True
    
    async def process_cli_agent(self, agent_name: str, config: Dict) -> bool:
        """Process a single CLI agent"""
        console.print(f"\n[bold]Processing {agent_name}...[/bold]")
        agent_dir = config["output_dir"]
        agent_dir.mkdir(exist_ok=True, parents=True)
        
        try:
            # Fetch release info
            release_data = await self.fetch_github_releases(config["repo"])
            
            # Find binary assets
            for asset in release_data.get("assets", []):
                asset_name = asset["name"].lower()
                if any(platform in asset_name for platform in config["platforms"]):
                    download_url = asset["browser_download_url"]
                    asset_path = agent_dir / asset["name"]
                    
                    console.print(f"Downloading {asset['name']}...")
                    if await self.download_binary(download_url, asset_path):
                        # Extract if archive
                        if any(ext in asset_name for ext in ARCHIVE_EXTENSIONS):
                            extract_dir = agent_dir / "extracted"
                            if self.extract_with_unpack_tools(asset_path, extract_dir):
                                # Find binary in extracted files
                                for binary in extract_dir.rglob("*"):
                                    if binary.is_file() and binary.stat().st_mode & 0o111:
                                        self.decompile_binary(binary, agent_dir / "analysis")
                        else:
                            # Direct binary
                            asset_path.chmod(0o755)
                            self.decompile_binary(asset_path, agent_dir / "analysis")
                    
            return True
            
        except Exception as e:
            console.print(f"[red]Error processing {agent_name}: {e}[/red]")
            return False

@app.command()
def setup():
    """Instantly prepare decomp/ directory and analyze CLI agents"""
    console.print("[bold]🔥 HOHO DECOMP SETUP - Reference CLI Agent Analysis[/bold]")
    console.print("[dim]Analyzing frontier CLI agents for research and reference...[/dim]")
    console.print("[yellow]⚖️  Operating in grey area of public acceptance with full transparency[/yellow]")
    console.print("[green]✨ Fair exchange - research and learning, not competitive replication[/green]\n")
    
    ensure_directories()
    decompile_all()

@app.command() 
def decompile_all():
    """Decompile all CLI agents"""
    decompiler = CliDecompiler()
    
    async def run_all():
        tasks = []
        for agent_name, config in CLI_AGENTS.items():
            tasks.append(decompiler.process_cli_agent(agent_name, config))
        
        results = await asyncio.gather(*tasks, return_exceptions=True)
        
        success_count = sum(1 for r in results if r is True)
        console.print(f"\n[bold green]Completed: {success_count}/{len(CLI_AGENTS)} agents processed[/bold green]")
    
    asyncio.run(run_all())

@app.command() 
def decompile_single(agent: str):
    """Decompile a single CLI agent"""
    if agent not in CLI_AGENTS:
        console.print(f"[red]Unknown agent: {agent}. Available: {list(CLI_AGENTS.keys())}[/red]")
        return
    
    decompiler = CliDecompiler()
    asyncio.run(decompiler.process_cli_agent(agent, CLI_AGENTS[agent]))

def main():
    """Entry point for hoho-decompile command"""
    app()

if __name__ == "__main__":
    main()