# Claude Code Architecture Analysis - Complete Reverse Engineering

## 🔍 Package Structure Overview

### Core Files
- **cli.js** (9.1MB) - Minified React-based TUI application bundle
- **sdk.mjs** (509KB) - SDK module for programmatic access  
- **sdk.d.ts** - TypeScript definitions for SDK
- **sdk-tools.d.ts** - Tool input schema definitions
- **yoga.wasm** (87KB) - Yoga layout engine for flexbox layout in terminal

### Bundled Binaries
- **ripgrep** - Fast file search (bundled for all platforms)
- **JetBrains plugin** - IDE integration (31+ JAR files)
- **Sharp image libraries** - Image processing capabilities

## 🏗️ Architecture Breakdown

### 1. **Frontend: React-based TUI**
The minified cli.js reveals a full React 18.3.1 application compiled for terminal:
- Uses React components for terminal UI rendering
- Implements custom terminal renderer (likely Ink or similar)
- Yoga WASM for flexbox layout calculations in terminal
- Event-driven architecture with hooks

### 2. **Tool System**
Complete tool inventory from sdk-tools.d.ts:

#### File Operations
- `FileRead` - Read files with offset/limit for large files
- `FileWrite` - Write content to files
- `FileEdit` - String-based find/replace (NOT line-based!)
- `FileMultiEdit` - Multiple edits in single transaction
- `NotebookEdit` - Jupyter notebook cell manipulation

#### Search & Discovery
- `Glob` - File pattern matching
- `Grep` - Ripgrep integration with multiline support
- `WebSearch` - Web search with domain filtering
- `WebFetch` - Fetch and process web content

#### Execution & Process Management
- `Bash` - Shell command execution with:
  - Background execution support
  - Sandbox mode for safe operations
  - Timeout controls (max 10 minutes)
- `BashOutput` - Read from background processes
- `KillShell` - Terminate background shells

#### Agent & Workflow
- `Agent` - Spawn subagents for complex tasks
- `TodoWrite` - Task management with status tracking
- `ExitPlanMode` - Planning mode for complex operations

#### MCP Integration
- `Mcp` - Model Context Protocol tools
- `ListMcpResources` - Discover MCP resources
- `ReadMcpResource` - Access MCP resources

### 3. **Communication Architecture**

#### API Integration
```typescript
import { Message, MessageParam, Usage } from '@anthropic-ai/sdk/resources'
```
- Direct Anthropic SDK integration
- Message streaming with usage tracking
- Non-nullable usage enforcement

#### MCP (Model Context Protocol)
```typescript
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js'
```
- Supports multiple transport types:
  - stdio (process communication)
  - SSE (Server-Sent Events)
  - HTTP
  - SDK (direct integration)

### 4. **Permission System**

Sophisticated permission architecture:
```typescript
type PermissionBehavior = 'allow' | 'deny' | 'ask'
type PermissionUpdateDestination = 'userSettings' | 'projectSettings' | 'localSettings' | 'session'
```

Features:
- Granular tool permissions
- Rule-based access control
- Directory-based restrictions
- Session vs persistent permissions

### 5. **Hook System**

Event-driven extensibility:
```typescript
HOOK_EVENTS = ["PreToolUse", "PostToolUse", "Notification", "UserPromptSubmit", 
               "SessionStart", "SessionEnd", "Stop", "SubagentStop", "PreCompact"]
```

Enables:
- Tool interception
- Session lifecycle management
- User interaction hooks
- Transcript management

## 🔄 Key Architectural Insights

### 1. **String-Based Editing (Superior to Line-Based)**
The `FileEdit` tool uses `old_string` → `new_string` replacement, NOT line numbers:
- Resilient to file changes
- Supports multi-line replacements
- Atomic operations with `replace_all` option

### 2. **React in Terminal (Performance Issue)**
The 9MB cli.js bundle contains full React:
- Explains reported lag issues
- Virtual DOM overhead in terminal
- Heavy bundling with all dependencies

### 3. **Sandbox Execution Model**
```typescript
sandbox?: boolean  // Commands can run in restricted mode
```
- Read-only filesystem access in sandbox
- No network in sandbox mode
- Smooth UX without permission prompts

### 4. **Background Process Management**
Sophisticated async execution:
- Named shell sessions
- Output streaming with regex filters
- Process lifecycle management

## 🎯 C# Port Strategy

### Required Libraries

#### TUI Options (Choose One)
1. **Terminal.Gui** - Full TUI framework with dialogs
2. **Spectre.Console** - Modern, lighter weight
3. **Custom Renderer** - Direct ANSI escape sequences

#### Core Dependencies
1. **System.CommandLine** - CLI parsing (already in use)
2. **System.Diagnostics.Process** - Shell execution
3. **System.Text.Json** - JSON serialization (already in use)
4. **Microsoft.Extensions.FileSystemGlobbing** - Glob patterns
5. **System.Text.RegularExpressions** - Grep functionality

#### API & Communication
1. **HttpClient** - API calls (already in use)
2. **System.IO.Pipes** - MCP stdio transport
3. **System.Net.Http.Json** - SSE support
4. **WebSocket** - Real-time communication

### Architecture Mapping

| Claude Code Component | C# Implementation |
|----------------------|-------------------|
| React TUI | Terminal.Gui or Spectre.Console.Live |
| Yoga Layout | Terminal.Gui built-in or custom grid |
| Tool System | Interface-based with attributes |
| MCP Protocol | Custom or port MCP SDK |
| Hooks | Event system with delegates |
| Permissions | Attribute-based with interceptors |
| Background Tasks | Task.Run with CancellationToken |
| File Operations | System.IO with transactional wrapper |

### Implementation Priorities

1. **Core Tool System** - File operations, bash execution
2. **TUI Framework** - Choose and implement base UI
3. **API Integration** - Anthropic SDK port or direct HTTP
4. **Permission System** - Simplified initially
5. **MCP Support** - Start with stdio transport
6. **Hook System** - Event-based extensibility

## 🚀 Next Steps

1. **Choose TUI Library**: Terminal.Gui offers most features but heavier
2. **Port Tool Interfaces**: Create C# interfaces matching TypeScript definitions
3. **Implement Core Tools**: Start with File*, Bash, Grep
4. **Build CLI Parser**: Extend existing System.CommandLine setup
5. **Add API Layer**: Direct HTTP or port SDK patterns

The obfuscation is minimal - just minification. The architecture is transparent and ready for 1:1 porting to C#.