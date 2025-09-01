using System.CommandLine;
using Hoho.Core;

namespace Hoho.Decomp;

/// <summary>
/// Commands for managing the LSP server daemon
/// </summary>
public class LspStartCommand : Command
{
    public LspStartCommand() : base("lsp-start", "Start the LSP server daemon")
    {
        this.SetHandler(async () =>
        {
            await LspServerDaemon.Instance.StartAsync();
        });
    }
}

public class LspStopCommand : Command
{
    public LspStopCommand() : base("lsp-stop", "Stop the LSP server daemon")
    {
        this.SetHandler(() =>
        {
            if (LspServerDaemon.IsRunning())
            {
                LspServerDaemon.Instance.Stop();
                Logger.Success("LSP server stopped");
            }
            else
            {
                Logger.Info("LSP server is not running");
            }
        });
    }
}

public class LspStatusCommand : Command
{
    public LspStatusCommand() : base("lsp-status", "Check LSP server status")
    {
        this.SetHandler(() =>
        {
            if (LspServerDaemon.IsRunning())
            {
                Logger.Success("LSP server is running");
            }
            else
            {
                Logger.Info("LSP server is not running");
                Logger.Info("Use 'hoho decomp lsp-start' to start it");
            }
        });
    }
}