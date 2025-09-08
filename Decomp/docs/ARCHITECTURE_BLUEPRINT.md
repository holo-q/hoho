# HOHO C# Architecture Blueprint - Claude Code 1:1 Port

## 🎯 Project Structure

```
hoho/
├── Core/
│   ├── Tools/           # Tool implementations
│   │   ├── IHohoTool.cs
│   │   ├── FileTools.cs
│   │   ├── BashTools.cs
│   │   ├── SearchTools.cs
│   │   └── WebTools.cs
│   ├── Permissions/
│   │   ├── PermissionManager.cs
│   │   └── PermissionRules.cs
│   ├── Hooks/
│   │   ├── HookManager.cs
│   │   └── HookEvents.cs
│   ├── Api/
│   │   ├── AnthropicClient.cs
│   │   └── MessageStream.cs
│   └── Session/
│       ├── SessionManager.cs
│       └── TranscriptManager.cs
├── Tui/
│   ├── Windows/
│   │   ├── MainWindow.cs
│   │   ├── ChatWindow.cs
│   │   └── ToolOutputWindow.cs
│   ├── Widgets/
│   │   ├── TodoListWidget.cs
│   │   ├── FileTreeWidget.cs
│   │   └── ProgressWidget.cs
│   └── TuiManager.cs
├── Mcp/
│   ├── McpServer.cs
│   ├── Transports/
│   │   ├── StdioTransport.cs
│   │   ├── SseTransport.cs
│   │   └── HttpTransport.cs
│   └── McpProtocol.cs
└── Program.cs
```

## 🔧 Core Components

### 1. Tool System

```csharp
public interface IHohoTool
{
    string Name { get; }
    string Description { get; }
    Type InputType { get; }
    Task<ToolResult> ExecuteAsync(object input, CancellationToken ct);
}

[Tool("file_read")]
public class FileReadTool : IHohoTool
{
    public async Task<ToolResult> ExecuteAsync(object input, CancellationToken ct)
    {
        var args = (FileReadInput)input;
        var content = await File.ReadAllTextAsync(args.FilePath, ct);
        
        if (args.Offset.HasValue || args.Limit.HasValue)
        {
            var lines = content.Split('\n');
            // Apply offset and limit
        }
        
        return new ToolResult { Success = true, Output = content };
    }
}
```

### 2. String-Based File Editing (Key Innovation)

```csharp
public class FileEditTool : IHohoTool
{
    public async Task<ToolResult> ExecuteAsync(object input, CancellationToken ct)
    {
        var args = (FileEditInput)input;
        var content = await File.ReadAllTextAsync(args.FilePath, ct);
        
        // String-based replacement (NOT line-based!)
        var newContent = args.ReplaceAll 
            ? content.Replace(args.OldString, args.NewString)
            : content.ReplaceFirst(args.OldString, args.NewString);
        
        if (content == newContent)
            return new ToolResult { Success = false, Error = "String not found" };
        
        await File.WriteAllTextAsync(args.FilePath, newContent, ct);
        return new ToolResult { Success = true };
    }
}
```

### 3. Bash Execution with Background Support

```csharp
public class BashTool : IHohoTool
{
    private readonly Dictionary<string, BackgroundShell> _shells = new();
    
    public async Task<ToolResult> ExecuteAsync(object input, CancellationToken ct)
    {
        var args = (BashInput)input;
        
        if (args.RunInBackground)
        {
            var shell = new BackgroundShell();
            var id = Guid.NewGuid().ToString();
            _shells[id] = shell;
            
            _ = Task.Run(() => shell.ExecuteAsync(args.Command, ct));
            
            return new ToolResult { 
                Success = true, 
                Output = $"Started background shell: {id}" 
            };
        }
        
        if (args.Sandbox)
        {
            // Run in restricted environment
            return await RunSandboxedAsync(args.Command, ct);
        }
        
        // Normal execution
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{args.Command}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        
        process.Start();
        
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        
        await process.WaitForExitAsync(ct);
        
        return new ToolResult
        {
            Success = process.ExitCode == 0,
            Output = output,
            Error = error
        };
    }
}
```

### 4. Terminal.Gui Integration

```csharp
public class HohoTui
{
    private Window mainWindow;
    private TextView chatView;
    private TextField inputField;
    private ListView toolOutputView;
    private TreeView fileTreeView;
    
    public void Initialize()
    {
        Application.Init();
        
        var top = Application.Top;
        
        // Main window
        mainWindow = new Window("HOHO - Shadow Protocol Active")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        
        // Chat area (70% width)
        chatView = new TextView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Percent(70),
            Height = Dim.Fill() - 3,
            ReadOnly = true
        };
        
        // Tool output (30% width)
        toolOutputView = new ListView()
        {
            X = Pos.Right(chatView),
            Y = 0,
            Width = Dim.Percent(30),
            Height = Dim.Percent(50)
        };
        
        // File tree (30% width, bottom)
        fileTreeView = new TreeView()
        {
            X = Pos.Right(chatView),
            Y = Pos.Bottom(toolOutputView),
            Width = Dim.Percent(30),
            Height = Dim.Fill() - 3
        };
        
        // Input field
        inputField = new TextField()
        {
            X = 0,
            Y = Pos.Bottom(chatView),
            Width = Dim.Fill(),
            Height = 3
        };
        
        mainWindow.Add(chatView, toolOutputView, fileTreeView, inputField);
        top.Add(mainWindow);
    }
}
```

### 5. Anthropic API Integration

```csharp
public class AnthropicClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    
    public async IAsyncEnumerable<StreamChunk> StreamCompletionAsync(
        List<Message> messages,
        List<ToolDefinition> tools,
        CancellationToken ct)
    {
        var request = new
        {
            model = "claude-3-5-sonnet-20241022",
            messages = messages,
            tools = tools,
            stream = true,
            max_tokens = 8192
        };
        
        using var response = await _http.PostAsStreamAsync(
            "https://api.anthropic.com/v1/messages",
            JsonContent.Create(request),
            ct);
        
        await foreach (var line in ReadSseStream(response, ct))
        {
            if (line.StartsWith("data: "))
            {
                var json = line[6..];
                var chunk = JsonSerializer.Deserialize<StreamChunk>(json);
                yield return chunk;
            }
        }
    }
}
```

### 6. Permission System

```csharp
public class PermissionManager
{
    private readonly List<PermissionRule> _rules = new();
    
    public async Task<PermissionResult> CheckPermissionAsync(
        string toolName, 
        object input,
        PermissionBehavior defaultBehavior)
    {
        var rule = _rules.FirstOrDefault(r => r.ToolName == toolName);
        
        if (rule == null)
        {
            if (defaultBehavior == PermissionBehavior.Ask)
            {
                return await PromptUserAsync(toolName, input);
            }
            return new PermissionResult { Allowed = defaultBehavior == PermissionBehavior.Allow };
        }
        
        return rule.Evaluate(input);
    }
    
    private async Task<PermissionResult> PromptUserAsync(string toolName, object input)
    {
        var dialog = new Dialog("Permission Request", 60, 10);
        dialog.AddButton("Allow", true);
        dialog.AddButton("Deny", false);
        
        Application.MainLoop.Invoke(() => {
            Application.Run(dialog);
        });
        
        return new PermissionResult { Allowed = dialog.Result };
    }
}
```

### 7. Hook System

```csharp
public enum HookEvent
{
    PreToolUse,
    PostToolUse,
    UserPromptSubmit,
    SessionStart,
    SessionEnd
}

public class HookManager
{
    private readonly Dictionary<HookEvent, List<Func<HookContext, Task>>> _hooks = new();
    
    public void Register(HookEvent evt, Func<HookContext, Task> handler)
    {
        if (!_hooks.ContainsKey(evt))
            _hooks[evt] = new();
        
        _hooks[evt].Add(handler);
    }
    
    public async Task TriggerAsync(HookEvent evt, HookContext context)
    {
        if (_hooks.TryGetValue(evt, out var handlers))
        {
            foreach (var handler in handlers)
            {
                await handler(context);
            }
        }
    }
}
```

### 8. MCP (Model Context Protocol) Support

```csharp
public class McpServer
{
    private readonly Dictionary<string, IMcpTransport> _transports = new();
    
    public async Task<McpServer> ConnectStdioAsync(string command, string[] args)
    {
        var transport = new StdioTransport();
        await transport.StartAsync(command, args);
        
        _transports[command] = transport;
        return this;
    }
    
    public async Task<ToolResult> CallToolAsync(string server, string tool, object input)
    {
        var transport = _transports[server];
        var request = new McpRequest
        {
            Method = "tools/call",
            Params = new { name = tool, arguments = input }
        };
        
        var response = await transport.SendAsync(request);
        return new ToolResult { Success = true, Output = response.Result };
    }
}
```

## 📦 NuGet Dependencies

```xml
<ItemGroup>
  <!-- Core -->
  <PackageReference Include="Terminal.Gui" Version="1.14.1" />
  <PackageReference Include="System.CommandLine" Version="2.0.0-beta4" />
  
  <!-- Serialization -->
  <PackageReference Include="System.Text.Json" Version="8.0.5" />
  
  <!-- File Operations -->
  <PackageReference Include="Microsoft.Extensions.FileSystemGlobbing" Version="8.0.0" />
  
  <!-- Logging -->
  <PackageReference Include="Serilog" Version="3.1.1" />
  <PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />
  
  <!-- HTTP -->
  <PackageReference Include="System.Net.Http.Json" Version="8.0.0" />
  
  <!-- Process Management -->
  <PackageReference Include="CliWrap" Version="3.6.4" /> <!-- Optional for better process handling -->
</ItemGroup>
```

## 🚀 Implementation Phases

### Phase 1: Core Foundation (Week 1)
- [ ] Basic TUI with Terminal.Gui
- [ ] File operations tools
- [ ] Bash execution tool
- [ ] Simple chat display

### Phase 2: API Integration (Week 2)
- [ ] Anthropic API client
- [ ] Message streaming
- [ ] Tool calling protocol
- [ ] Response rendering

### Phase 3: Advanced Tools (Week 3)
- [ ] Grep/search implementation
- [ ] Web tools
- [ ] Background processes
- [ ] Todo management

### Phase 4: Permission & Hooks (Week 4)
- [ ] Permission system
- [ ] Hook architecture
- [ ] User prompts
- [ ] Settings management

### Phase 5: MCP & Polish (Week 5)
- [ ] MCP protocol implementation
- [ ] Multi-transport support
- [ ] Performance optimization
- [ ] Testing & debugging

## 🎨 Key Design Decisions

1. **Terminal.Gui over React**: 10-20x performance improvement
2. **String-based editing**: More robust than line numbers
3. **Native process management**: Direct Process class usage
4. **Async throughout**: Modern C# async/await patterns
5. **Plugin architecture**: Hook system for extensibility
6. **Source generation**: For JSON serialization (AOT ready)

## 🔥 Performance Targets

- Startup: < 100ms (vs ~2s for Claude Code)
- Memory: < 50MB (vs 150-200MB)
- Response latency: < 10ms for UI updates
- Tool execution: Native speed
- No virtual DOM overhead

This architecture provides a complete 1:1 feature port while dramatically improving performance through native C# implementation.