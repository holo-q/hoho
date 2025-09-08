using Hoho.Core.Sandbox;

namespace Hoho.Core.Tools;

public sealed class FileService
{
    private readonly string _root;
    private readonly SandboxMode _mode;
    public FileService(string root, SandboxMode mode = SandboxMode.WorkspaceWrite)
    {
        _root = Path.GetFullPath(root);
        _mode = mode;
    }

    private string ResolveSafe(string relPath)
    {
        if (Path.IsPathRooted(relPath)) throw new InvalidOperationException("Absolute paths not allowed");
        var full = Path.GetFullPath(Path.Combine(_root, relPath));
        if (!full.StartsWith(_root, StringComparison.Ordinal)) throw new InvalidOperationException("Path escapes workspace root");
        return full;
    }

    public async Task<string> ReadTextAsync(string relPath) => await File.ReadAllTextAsync(ResolveSafe(relPath));
    public async Task WriteTextAsync(string relPath, string content)
    {
        if (_mode == SandboxMode.ReadOnly) throw new InvalidOperationException("Sandbox read-only: write disabled");
        var full = ResolveSafe(relPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, content);
    }

    public async Task<byte[]> ReadBytesAsync(string relPath) => await File.ReadAllBytesAsync(ResolveSafe(relPath));
    public async Task WriteBytesAsync(string relPath, byte[] content)
    {
        if (_mode == SandboxMode.ReadOnly) throw new InvalidOperationException("Sandbox read-only: write disabled");
        var full = ResolveSafe(relPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, content);
    }

    public void Delete(string relPath)
    {
        if (_mode == SandboxMode.ReadOnly) throw new InvalidOperationException("Sandbox read-only: delete disabled");
        var full = ResolveSafe(relPath);
        if (File.Exists(full)) File.Delete(full);
    }

    public void Move(string fromRelPath, string toRelPath)
    {
        if (_mode == SandboxMode.ReadOnly) throw new InvalidOperationException("Sandbox read-only: move disabled");
        var from = ResolveSafe(fromRelPath);
        var to = ResolveSafe(toRelPath);
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        File.Move(from, to, overwrite: true);
    }
}
