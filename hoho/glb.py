"""
Global paths, constants, and configuration for hoho
"""

from pathlib import Path
from typing import Dict, List

# Base directories
PROJECT_ROOT = Path(__file__).parent.parent
DECOMP_DIR = PROJECT_ROOT / "decomp"
CACHE_DIR = PROJECT_ROOT / ".cache"
TEMP_DIR = PROJECT_ROOT / ".tmp"

# CLI Agent configurations
CLI_AGENTS: Dict[str, Dict] = {
    "claude-code": {
        "repo": "anthropics/claude-code", 
        "binary_name": "claude-code",
        "install_url": "https://github.com/anthropics/claude-code/releases/latest",
        "platforms": ["linux", "darwin", "win32"],
        "output_dir": DECOMP_DIR / "claude-code"
    },
    "openai-codex": {
        "repo": "openai/openai-python",
        "binary_name": "openai", 
        "install_url": "https://pypi.org/project/openai/",
        "platforms": ["universal"],
        "output_dir": DECOMP_DIR / "openai-codex"
    },
    "gemini-cli": {
        "repo": "google/generative-ai-python",
        "binary_name": "genai",
        "install_url": "https://pypi.org/project/google-generativeai/",  
        "platforms": ["universal"],
        "output_dir": DECOMP_DIR / "gemini-cli"
    }
}

# Analysis tools
DECOMPILER_TOOLS: List[List[str]] = [
    ["strings"],     # Extract strings
    ["objdump", "-D"],  # Disassemble  
    ["nm"],          # List symbols
    ["readelf", "-a"], # ELF analysis
    ["hexdump", "-C"], # Hex dump
]

# File extensions to extract/analyze
ARCHIVE_EXTENSIONS = [".tar.gz", ".zip", ".tar", ".tar.xz", ".7z"]
BINARY_EXTENSIONS = [".exe", ".bin", ".so", ".dll", ".dylib"]

def ensure_directories():
    """Ensure all required directories exist"""
    for directory in [DECOMP_DIR, CACHE_DIR, TEMP_DIR]:
        directory.mkdir(exist_ok=True, parents=True)
    
    # Ensure agent directories exist
    for agent_config in CLI_AGENTS.values():
        agent_config["output_dir"].mkdir(exist_ok=True, parents=True)