using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hoho.Core;

namespace Hoho.Decomp;

/// <summary>
/// Persistent LSP server daemon that stays running between rename operations
/// </summary>
public class LspServerDaemon
{
    private static LspServerDaemon? _instance;
    private readonly int _port;
    private TcpListener? _tcpListener;
    private LspRenameService? _lspService;
    private bool _isRunning;
    private Task? _listenTask;
    
    private const string LOCK_FILE = "decomp/.lsp-server.lock";
    private const string DEFAULT_WORKSPACE = "decomp/claude-code-dev/versions/1.0.98";
    
    public static LspServerDaemon Instance => _instance ??= new LspServerDaemon();
    
    private LspServerDaemon()
    {
        _port = 9876; // Fixed port for simplicity
    }
    
    /// <summary>
    /// Start the LSP server daemon
    /// </summary>
    public async Task StartAsync()
    {
        if (_isRunning)
        {
            Logger.Info("LSP server already running");
            return;
        }
        
        // Check if another instance is running
        if (File.Exists(LOCK_FILE))
        {
            try
            {
                var pidStr = await File.ReadAllTextAsync(LOCK_FILE);
                if (int.TryParse(pidStr, out var pid))
                {
                    var process = Process.GetProcessById(pid);
                    if (process != null && !process.HasExited)
                    {
                        Logger.Warning($"LSP server already running (PID: {pid})");
                        return;
                    }
                }
            }
            catch
            {
                // Process doesn't exist, clean up lock file
                File.Delete(LOCK_FILE);
            }
        }
        
        // Write lock file
        Directory.CreateDirectory(Path.GetDirectoryName(LOCK_FILE)!);
        await File.WriteAllTextAsync(LOCK_FILE, Process.GetCurrentProcess().Id.ToString());
        
        // Start LSP service
        Logger.Info("Starting TypeScript Language Server...");
        _lspService = new LspRenameService(Path.GetFullPath(DEFAULT_WORKSPACE));
        await _lspService.InitializeAsync();
        
        // Start TCP listener
        _tcpListener = new TcpListener(IPAddress.Loopback, _port);
        _tcpListener.Start();
        _isRunning = true;
        
        Logger.Success($"LSP server running on port {_port}");
        Logger.Info("Use 'hoho decomp lsp-stop' to stop the server");
        
        // Start listening for connections
        _listenTask = Task.Run(ListenForClients);
    }
    
    /// <summary>
    /// Stop the LSP server daemon
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;
        
        _isRunning = false;
        _tcpListener?.Stop();
        _lspService?.Dispose();
        
        if (File.Exists(LOCK_FILE))
        {
            File.Delete(LOCK_FILE);
        }
        
        Logger.Info("LSP server stopped");
    }
    
    private async Task ListenForClients()
    {
        while (_isRunning)
        {
            try
            {
                var tcpClient = await _tcpListener!.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClient(tcpClient));
            }
            catch when (!_isRunning)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error accepting client: {ex.Message}");
            }
        }
    }
    
    private async Task HandleClient(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var reader = new StreamReader(stream);
                var writer = new StreamWriter(stream) { AutoFlush = true };
                
                // Read request
                var requestJson = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(requestJson))
                {
                    return;
                }
                
                var request = JsonSerializer.Deserialize<RenameRequest>(requestJson);
                if (request == null)
                {
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new RenameResponse
                    {
                        Success = false,
                        Error = "Invalid request"
                    }));
                    return;
                }
                
                // Process rename
                var response = await ProcessRenameAsync(request);
                
                // Send response
                await writer.WriteLineAsync(JsonSerializer.Serialize(response));
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling client: {ex.Message}");
            }
        }
    }
    
    private async Task<RenameResponse> ProcessRenameAsync(RenameRequest request)
    {
        try
        {
            // Open file if needed
            await _lspService!.OpenFileAsync(request.FilePath);
            
            // Perform renames
            var report = await _lspService.BatchRenameAsync(request.FilePath, request.Mappings);
            
            return new RenameResponse
            {
                Success = true,
                SuccessfulRenames = report.SuccessfulRenames,
                FailedRenames = report.FailedRenames,
                TotalReferences = report.TotalReferences
            };
        }
        catch (Exception ex)
        {
            return new RenameResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
    
    /// <summary>
    /// Check if server is running
    /// </summary>
    public static bool IsRunning()
    {
        if (!File.Exists(LOCK_FILE)) return false;
        
        try
        {
            var pidStr = File.ReadAllText(LOCK_FILE);
            if (int.TryParse(pidStr, out var pid))
            {
                var process = Process.GetProcessById(pid);
                return process != null && !process.HasExited;
            }
        }
        catch
        {
            // Process doesn't exist
        }
        
        return false;
    }
}

/// <summary>
/// Client for communicating with LSP server daemon
/// </summary>
public class LspClient
{
    private const int PORT = 9876;
    
    public static async Task<RenameResponse> RenameAsync(string filePath, Dictionary<string, string> mappings)
    {
        // Check if server is running
        if (!LspServerDaemon.IsRunning())
        {
            Logger.Info("LSP server not running, starting it now...");
            await LspServerDaemon.Instance.StartAsync();
            await Task.Delay(2000); // Give it time to start
        }
        
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, PORT);
            
            var stream = client.GetStream();
            var writer = new StreamWriter(stream) { AutoFlush = true };
            var reader = new StreamReader(stream);
            
            // Send request
            var request = new RenameRequest
            {
                FilePath = filePath,
                Mappings = mappings
            };
            
            await writer.WriteLineAsync(JsonSerializer.Serialize(request));
            
            // Read response
            var responseJson = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(responseJson))
            {
                throw new Exception("No response from LSP server");
            }
            
            var response = JsonSerializer.Deserialize<RenameResponse>(responseJson);
            return response ?? throw new Exception("Invalid response from LSP server");
        }
        catch (SocketException)
        {
            Logger.Error("Could not connect to LSP server");
            Logger.Info("Try running 'hoho decomp lsp-start' first");
            throw;
        }
    }
}

// Request/Response models
public class RenameRequest
{
    public string FilePath { get; set; } = "";
    public Dictionary<string, string> Mappings { get; set; } = new();
}

public class RenameResponse
{
    public bool Success { get; set; }
    public int SuccessfulRenames { get; set; }
    public int FailedRenames { get; set; }
    public int TotalReferences { get; set; }
    public string? Error { get; set; }
}