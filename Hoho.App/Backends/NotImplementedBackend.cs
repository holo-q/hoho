namespace Hoho;

internal sealed class NotImplementedBackend : ITuiBackend
{
    public int Run(string workdir, string providerName, string? sessionId, string? initialPrompt = null, int? smokeMs = null)
    {
        Console.Error.WriteLine("Ratatui backend not yet implemented in this process. Use --backend terminal-gui (default)." );
        return 2;
    }
}

