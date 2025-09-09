namespace Hoho;

internal interface ITuiBackend
{
    int Run(string workdir, string providerName, string? sessionId, string? initialPrompt = null, int? smokeMs = null);
}

