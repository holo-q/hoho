using System.Diagnostics;
using Hoho.Core.Abstractions;

namespace Hoho.Core.Services;

public sealed class ShellRunner : IShellRunner
{
    public async IAsyncEnumerable<ShellChunk> RunAsync(
        IReadOnlyList<string> command,
        ShellOptions options,
        CancellationToken ct = default)
    {
        if (command.Count == 0) yield break;
        var psi = new ProcessStartInfo
        {
            FileName = command[0],
            WorkingDirectory = options.WorkDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
        };
        for (int i = 1; i < command.Count; i++) psi.ArgumentList.Add(command[i]);

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.Start();

        var exited = new TaskCompletionSource();
        proc.Exited += (_, _) => exited.TrySetResult();

        var stdout = Task.Run(async () =>
        {
            string? line;
            while ((line = await proc.StandardOutput.ReadLineAsync()) is not null)
            {
                yield return new ShellChunk("stdout", line + "\n");
            }
        });

        var stderr = Task.Run(async () =>
        {
            string? line;
            while ((line = await proc.StandardError.ReadLineAsync()) is not null)
            {
                yield return new ShellChunk("stderr", line + "\n");
            }
        });

        await Task.WhenAll(exited.Task, stdout, stderr);
    }
}

