using System.Diagnostics;
using Hoho.Core.Sandbox;
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
        // Sandbox checks
        if (options.WorkDir is { Length: > 0 } && options.AllowedRoot is { Length: > 0 })
        {
            var wd = Path.GetFullPath(options.WorkDir);
            var root = Path.GetFullPath(options.AllowedRoot);
            if (!wd.StartsWith(root, StringComparison.Ordinal))
                throw new InvalidOperationException("ShellRunner: workdir escapes allowed root");
        }
        if (!options.AllowNetwork && IsNetworkCommand(command))
        {
            throw new InvalidOperationException("ShellRunner: network commands blocked by sandbox");
        }
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

    private static bool IsNetworkCommand(IReadOnlyList<string> cmd)
    {
        if (cmd.Count == 0) return false;
        var exe = Path.GetFileName(cmd[0]).ToLowerInvariant();
        var blocked = new[] { "curl", "wget", "pip", "npm", "pnpm", "yarn", "apt", "apt-get", "brew", "git" };
        if (blocked.Contains(exe))
        {
            // Special-case git: allow 'git status' and 'git diff' locally
            if (exe == "git" && cmd.Count >= 2)
            {
                var sub = cmd[1].ToLowerInvariant();
                if (sub == "status" || sub == "diff" || sub == "blame") return false;
            }
            return true;
        }
        return false;
    }
}
