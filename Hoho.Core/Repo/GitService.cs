using Hoho.Core.Abstractions;

namespace Hoho.Core.Repo;

public sealed class GitService
{
    private readonly IShellRunner _shell;
    private readonly string _workdir;
    public GitService(IShellRunner shell, string workdir)
    {
        _shell = shell; _workdir = workdir;
    }

    public async Task<string> StatusAsync(CancellationToken ct = default)
    {
        var sb = new System.Text.StringBuilder();
        await foreach (var c in _shell.RunAsync(new[] { "git", "status", "--porcelain=v1" }, new ShellOptions { WorkDir = _workdir }, ct))
        {
            if (c.Stream == "stdout") sb.Append(c.Data);
        }
        return sb.ToString();
    }

    public async Task<string> DiffAsync(string? path = null, CancellationToken ct = default)
    {
        var args = new List<string> { "git", "diff" };
        if (!string.IsNullOrWhiteSpace(path)) args.Add(path!);
        var sb = new System.Text.StringBuilder();
        await foreach (var c in _shell.RunAsync(args, new ShellOptions { WorkDir = _workdir }, ct))
        {
            if (c.Stream == "stdout") sb.Append(c.Data);
        }
        return sb.ToString();
    }

    public async Task CommitAllAsync(string message, CancellationToken ct = default)
    {
        await foreach (var _ in _shell.RunAsync(new[] { "git", "add", "-A" }, new ShellOptions { WorkDir = _workdir }, ct)) { }
        await foreach (var _ in _shell.RunAsync(new[] { "git", "commit", "-m", message }, new ShellOptions { WorkDir = _workdir }, ct)) { }
    }
}

