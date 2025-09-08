namespace Hoho.Core.Abstractions;

public interface IShellRunner
{
    IAsyncEnumerable<ShellChunk> RunAsync(
        IReadOnlyList<string> command,
        ShellOptions options,
        CancellationToken ct = default);
}

public readonly record struct ShellChunk(string Stream, string Data);

public record ShellOptions
{
    public string WorkDir { get; init; } = Directory.GetCurrentDirectory();
    public bool WithEscalatedPermissions { get; init; }
    public bool AllowNetwork { get; init; }
    public TimeSpan? Timeout { get; init; }
}

