using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hoho.Core;

namespace Hoho.Decomp;

/// <summary>
/// LSP-based structural renaming service using TypeScript Language Server.
/// Provides IDE-quality symbol renaming with full scope and reference awareness.
/// </summary>
public class LspRenameService : IDisposable
{
    private Process? _lspProcess;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private int _requestId = 0;
    private readonly string _workspaceRoot;
    private readonly Dictionary<int, TaskCompletionSource<JsonNode>> _pendingRequests = new();
    private Task? _readTask;
    private CancellationTokenSource _cancellationSource = new();
    
    public LspRenameService(string workspaceRoot)
    {
        _workspaceRoot = Path.GetFullPath(workspaceRoot);
    }
    
    /// <summary>
    /// Initialize the TypeScript Language Server
    /// </summary>
    public async Task InitializeAsync()
    {
        using var timer = Logger.TimeOperation("Initialize LSP");
        
        // Start TypeScript language server
        _lspProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "npx",
                Arguments = "typescript-language-server --stdio",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = _workspaceRoot
            }
        };
        
        _lspProcess.Start();
        _stdin = _lspProcess.StandardInput;
        _stdout = _lspProcess.StandardOutput;
        
        // Start reading responses
        _readTask = Task.Run(() => ReadLoop(_cancellationSource.Token));
        
        // Send initialize request
        var initRequest = new
        {
            jsonrpc = "2.0",
            id = GetNextId(),
            method = "initialize",
            @params = new
            {
                processId = Process.GetCurrentProcess().Id,
                rootUri = new Uri(_workspaceRoot).ToString(),
                capabilities = new
                {
                    textDocument = new
                    {
                        rename = new
                        {
                            dynamicRegistration = true,
                            prepareSupport = true
                        },
                        references = new
                        {
                            dynamicRegistration = true
                        }
                    },
                    workspace = new
                    {
                        applyEdit = true,
                        workspaceEdit = new
                        {
                            documentChanges = true
                        }
                    }
                },
                initializationOptions = new
                {
                    preferences = new
                    {
                        includeInlayParameterNameHints = "none",
                        includeInlayParameterNameHintsWhenArgumentMatchesName = false,
                        includeInlayFunctionParameterTypeHints = false,
                        includeInlayVariableTypeHints = false,
                        includeInlayPropertyDeclarationTypeHints = false,
                        includeInlayFunctionLikeReturnTypeHints = false,
                        includeInlayEnumMemberValueHints = false
                    }
                }
            }
        };
        
        var response = await SendRequestAsync(initRequest);
        
        // Send initialized notification
        await SendNotificationAsync(new
        {
            jsonrpc = "2.0",
            method = "initialized",
            @params = new { }
        });
        
        Logger.Success("TypeScript LSP initialized");
    }
    
    /// <summary>
    /// Open a JavaScript file in the LSP
    /// </summary>
    public async Task OpenFileAsync(string filePath)
    {
        var uri = new Uri(Path.GetFullPath(filePath)).ToString();
        var content = await File.ReadAllTextAsync(filePath);
        
        await SendNotificationAsync(new
        {
            jsonrpc = "2.0",
            method = "textDocument/didOpen",
            @params = new
            {
                textDocument = new
                {
                    uri = uri,
                    languageId = "javascript",
                    version = 1,
                    text = content
                }
            }
        });
    }
    
    /// <summary>
    /// Find all references to a symbol at a given position
    /// </summary>
    public async Task<List<Location>> FindReferencesAsync(string filePath, int line, int character)
    {
        var uri = new Uri(Path.GetFullPath(filePath)).ToString();
        
        var request = new
        {
            jsonrpc = "2.0",
            id = GetNextId(),
            method = "textDocument/references",
            @params = new
            {
                textDocument = new { uri = uri },
                position = new { line = line, character = character },
                context = new { includeDeclaration = true }
            }
        };
        
        var response = await SendRequestAsync(request);
        var locations = new List<Location>();
        
        if (response?["result"] is JsonArray results)
        {
            foreach (var item in results)
            {
                locations.Add(new Location
                {
                    Uri = item?["uri"]?.GetValue<string>() ?? "",
                    Range = new Range
                    {
                        Start = new Position
                        {
                            Line = item?["range"]?["start"]?["line"]?.GetValue<int>() ?? 0,
                            Character = item?["range"]?["start"]?["character"]?.GetValue<int>() ?? 0
                        },
                        End = new Position
                        {
                            Line = item?["range"]?["end"]?["line"]?.GetValue<int>() ?? 0,
                            Character = item?["range"]?["end"]?["character"]?.GetValue<int>() ?? 0
                        }
                    }
                });
            }
        }
        
        return locations;
    }
    
    /// <summary>
    /// Rename a symbol at a given position
    /// </summary>
    public async Task<WorkspaceEdit?> RenameSymbolAsync(string filePath, int line, int character, string newName)
    {
        var uri = new Uri(Path.GetFullPath(filePath)).ToString();
        
        // First, check if rename is valid at this position
        var prepareRequest = new
        {
            jsonrpc = "2.0",
            id = GetNextId(),
            method = "textDocument/prepareRename",
            @params = new
            {
                textDocument = new { uri = uri },
                position = new { line = line, character = character }
            }
        };
        
        var prepareResponse = await SendRequestAsync(prepareRequest);
        if (prepareResponse?["result"] == null)
        {
            Logger.Warning($"Cannot rename at position {line}:{character}");
            return null;
        }
        
        // Perform the rename
        var renameRequest = new
        {
            jsonrpc = "2.0",
            id = GetNextId(),
            method = "textDocument/rename",
            @params = new
            {
                textDocument = new { uri = uri },
                position = new { line = line, character = character },
                newName = newName
            }
        };
        
        var response = await SendRequestAsync(renameRequest);
        if (response?["result"] == null)
        {
            return null;
        }
        
        return ParseWorkspaceEdit(response["result"]);
    }
    
    /// <summary>
    /// Apply a workspace edit (actually modify files)
    /// </summary>
    public async Task ApplyWorkspaceEditAsync(WorkspaceEdit edit)
    {
        foreach (var change in edit.Changes)
        {
            var filePath = new Uri(change.Key).LocalPath;
            var content = await File.ReadAllTextAsync(filePath);
            
            // Apply edits in reverse order to maintain positions
            foreach (var textEdit in change.Value.OrderByDescending(e => e.Range.Start.Line)
                                                  .ThenByDescending(e => e.Range.Start.Character))
            {
                content = ApplyTextEdit(content, textEdit);
            }
            
            await File.WriteAllTextAsync(filePath, content);
            Logger.Info($"Applied {change.Value.Count} edits to {Path.GetFileName(filePath)}");
        }
    }
    
    /// <summary>
    /// Batch rename symbols based on a mapping
    /// </summary>
    public async Task<RenameReport> BatchRenameAsync(string filePath, Dictionary<string, string> symbolMap)
    {
        var report = new RenameReport { FilePath = filePath };
        
        // Open the file
        await OpenFileAsync(filePath);
        
        // Read file content to find symbol positions
        var content = await File.ReadAllTextAsync(filePath);
        var lines = content.Split('\n');
        
        foreach (var mapping in symbolMap)
        {
            var oldName = mapping.Key;
            var newName = mapping.Value;
            
            // Find symbol positions (this is simplified - real implementation would use proper parsing)
            var positions = FindSymbolPositions(lines, oldName);
            
            foreach (var pos in positions)
            {
                try
                {
                    // Find all references
                    var references = await FindReferencesAsync(filePath, pos.Line, pos.Character);
                    report.TotalReferences += references.Count;
                    
                    // Perform rename
                    var edit = await RenameSymbolAsync(filePath, pos.Line, pos.Character, newName);
                    if (edit != null)
                    {
                        await ApplyWorkspaceEditAsync(edit);
                        report.SuccessfulRenames++;
                        Logger.Success($"Renamed {oldName} → {newName} ({references.Count} references)");
                    }
                    else
                    {
                        report.FailedRenames++;
                        Logger.Warning($"Failed to rename {oldName} → {newName}");
                    }
                    
                    break; // Only need to rename once per symbol
                }
                catch (Exception ex)
                {
                    report.FailedRenames++;
                    Logger.Error($"Error renaming {oldName}: {ex.Message}");
                }
            }
        }
        
        return report;
    }
    
    private List<Position> FindSymbolPositions(string[] lines, string symbolName)
    {
        var positions = new List<Position>();
        
        // Simple regex patterns for common symbol definitions
        var patterns = new[]
        {
            $@"\bclass\s+{symbolName}\b",
            $@"\bfunction\s+{symbolName}\b",
            $@"\bvar\s+{symbolName}\b",
            $@"\blet\s+{symbolName}\b",
            $@"\bconst\s+{symbolName}\b",
        };
        
        for (int lineNum = 0; lineNum < lines.Length; lineNum++)
        {
            var line = lines[lineNum];
            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, pattern);
                if (match.Success)
                {
                    positions.Add(new Position
                    {
                        Line = lineNum,
                        Character = match.Index + match.Value.LastIndexOf(symbolName)
                    });
                }
            }
        }
        
        return positions;
    }
    
    private string ApplyTextEdit(string content, TextEdit edit)
    {
        var lines = content.Split('\n').ToList();
        
        // Handle multi-line edits
        if (edit.Range.Start.Line == edit.Range.End.Line)
        {
            // Single line edit
            var line = lines[edit.Range.Start.Line];
            var before = line.Substring(0, edit.Range.Start.Character);
            var after = line.Substring(edit.Range.End.Character);
            lines[edit.Range.Start.Line] = before + edit.NewText + after;
        }
        else
        {
            // Multi-line edit
            var startLine = lines[edit.Range.Start.Line];
            var endLine = lines[edit.Range.End.Line];
            
            var before = startLine.Substring(0, edit.Range.Start.Character);
            var after = endLine.Substring(edit.Range.End.Character);
            
            // Remove the lines being replaced
            lines.RemoveRange(edit.Range.Start.Line, edit.Range.End.Line - edit.Range.Start.Line + 1);
            
            // Insert the new text
            lines.Insert(edit.Range.Start.Line, before + edit.NewText + after);
        }
        
        return string.Join('\n', lines);
    }
    
    private WorkspaceEdit ParseWorkspaceEdit(JsonNode node)
    {
        var edit = new WorkspaceEdit();
        
        if (node["changes"] is JsonObject changes)
        {
            foreach (var kvp in changes)
            {
                var uri = kvp.Key;
                var edits = new List<TextEdit>();
                
                if (kvp.Value is JsonArray editArray)
                {
                    foreach (var item in editArray)
                    {
                        edits.Add(new TextEdit
                        {
                            Range = new Range
                            {
                                Start = new Position
                                {
                                    Line = item?["range"]?["start"]?["line"]?.GetValue<int>() ?? 0,
                                    Character = item?["range"]?["start"]?["character"]?.GetValue<int>() ?? 0
                                },
                                End = new Position
                                {
                                    Line = item?["range"]?["end"]?["line"]?.GetValue<int>() ?? 0,
                                    Character = item?["range"]?["end"]?["character"]?.GetValue<int>() ?? 0
                                }
                            },
                            NewText = item?["newText"]?.GetValue<string>() ?? ""
                        });
                    }
                }
                
                edit.Changes[uri] = edits;
            }
        }
        
        return edit;
    }
    
    private async Task<JsonNode?> SendRequestAsync(object request)
    {
        var id = GetIdFromRequest(request);
        var tcs = new TaskCompletionSource<JsonNode>();
        _pendingRequests[id] = tcs;
        
        await SendMessageAsync(request);
        
        // Wait for response with timeout
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
        var responseTask = tcs.Task;
        
        var completedTask = await Task.WhenAny(responseTask, timeoutTask);
        if (completedTask == timeoutTask)
        {
            _pendingRequests.Remove(id);
            throw new TimeoutException($"LSP request {id} timed out");
        }
        
        return await responseTask;
    }
    
    private async Task SendNotificationAsync(object notification)
    {
        await SendMessageAsync(notification);
    }
    
    private async Task SendMessageAsync(object message)
    {
        var json = JsonSerializer.Serialize(message);
        var contentLength = System.Text.Encoding.UTF8.GetByteCount(json);
        
        await _stdin!.WriteLineAsync($"Content-Length: {contentLength}");
        await _stdin.WriteLineAsync();
        await _stdin.WriteAsync(json);
        await _stdin.FlushAsync();
    }
    
    private async Task ReadLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Read headers
                var headers = new Dictionary<string, string>();
                string? line;
                while (!string.IsNullOrEmpty(line = await _stdout!.ReadLineAsync()))
                {
                    var parts = line.Split(": ");
                    if (parts.Length == 2)
                    {
                        headers[parts[0]] = parts[1];
                    }
                }
                
                if (headers.TryGetValue("Content-Length", out var lengthStr) && 
                    int.TryParse(lengthStr, out var length))
                {
                    // Read content
                    var buffer = new char[length];
                    await _stdout.ReadBlockAsync(buffer, 0, length);
                    var json = new string(buffer);
                    
                    var node = JsonNode.Parse(json);
                    if (node != null)
                    {
                        ProcessResponse(node);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"LSP read error: {ex.Message}");
            }
        }
    }
    
    private void ProcessResponse(JsonNode response)
    {
        // Check if it's a response to our request
        if (response["id"] != null)
        {
            var id = response["id"]!.GetValue<int>();
            if (_pendingRequests.TryGetValue(id, out var tcs))
            {
                _pendingRequests.Remove(id);
                tcs.SetResult(response);
            }
        }
        // Handle notifications from server
        else if (response["method"] != null)
        {
            var method = response["method"]!.GetValue<string>();
            // Log server notifications if needed
            if (method != "$/progress" && method != "window/logMessage")
            {
                Logger.Debug($"LSP notification: {method}");
            }
        }
    }
    
    private int GetNextId() => ++_requestId;
    
    private int GetIdFromRequest(object request)
    {
        var json = JsonSerializer.Serialize(request);
        var node = JsonNode.Parse(json);
        return node?["id"]?.GetValue<int>() ?? 0;
    }
    
    public void Dispose()
    {
        _cancellationSource.Cancel();
        _readTask?.Wait(TimeSpan.FromSeconds(2));
        
        // Send shutdown request
        if (_lspProcess != null && !_lspProcess.HasExited)
        {
            SendNotificationAsync(new
            {
                jsonrpc = "2.0",
                method = "exit"
            }).Wait(TimeSpan.FromSeconds(1));
            
            _lspProcess.Kill();
            _lspProcess.Dispose();
        }
        
        _stdin?.Dispose();
        _stdout?.Dispose();
        _cancellationSource.Dispose();
    }
}

public class Location
{
    public string Uri { get; set; } = "";
    public Range Range { get; set; } = new();
}

public class Range
{
    public Position Start { get; set; } = new();
    public Position End { get; set; } = new();
}

public class Position
{
    public int Line { get; set; }
    public int Character { get; set; }
}

public class WorkspaceEdit
{
    public Dictionary<string, List<TextEdit>> Changes { get; set; } = new();
}

public class TextEdit
{
    public Range Range { get; set; } = new();
    public string NewText { get; set; } = "";
}

public class RenameReport
{
    public string FilePath { get; set; } = "";
    public int SuccessfulRenames { get; set; }
    public int FailedRenames { get; set; }
    public int TotalReferences { get; set; }
}