using System.Text;

namespace Hoho.Core.Tools;

public sealed class PatchService
{
    private readonly FileService _files;
    public PatchService(FileService files) { _files = files; }

    public async Task ApplyAsync(string patchText, CancellationToken ct = default)
    {
        using var reader = new StringReader(patchText);
        string? line;
        string? op = null; // Add File | Update File | Delete File
        string? path = null;
        string? moveTo = null;
        var buffer = new StringBuilder();

        void FlushPending()
        {
            if (op is null || path is null) return;
            switch (op)
            {
                case "Add File":
                    _ = _files.WriteTextAsync(path, buffer.ToString());
                    break;
                case "Update File":
                    if (moveTo is not null)
                    {
                        _files.Move(path, moveTo);
                        path = moveTo;
                    }
                    _ = _files.WriteTextAsync(path, buffer.ToString());
                    break;
                case "Delete File":
                    _files.Delete(path);
                    break;
            }
        }

        while ((line = reader.ReadLine()) is not null)
        {
            ct.ThrowIfCancellationRequested();
            if (line.StartsWith("*** Begin Patch"))
            {
                continue;
            }
            if (line.StartsWith("*** End Patch"))
            {
                break;
            }
            if (line.StartsWith("*** Add File: "))
            {
                FlushPending();
                op = "Add File";
                path = line.Substring("*** Add File: ".Length).Trim();
                moveTo = null;
                buffer.Clear();
                continue;
            }
            if (line.StartsWith("*** Update File: "))
            {
                FlushPending();
                op = "Update File";
                path = line.Substring("*** Update File: ".Length).Trim();
                moveTo = null;
                buffer.Clear();
                continue;
            }
            if (line.StartsWith("*** Delete File: "))
            {
                FlushPending();
                op = "Delete File";
                path = line.Substring("*** Delete File: ".Length).Trim();
                moveTo = null;
                buffer.Clear();
                continue;
            }
            if (line.StartsWith("*** Move to: "))
            {
                moveTo = line.Substring("*** Move to: ".Length).Trim();
                continue;
            }
            if (op == "Add File" || op == "Update File")
            {
                if (line.Length > 0 && (line[0] == '+' || line[0] == ' '))
                {
                    // Treat both '+' and ' ' as content lines for simplicity
                    buffer.AppendLine(line.Substring(1));
                    continue;
                }
                if (line.StartsWith("@@"))
                {
                    // Hunk headers not supported in this minimal implementation
                    throw new NotSupportedException("Hunked updates are not yet supported");
                }
            }
        }

        FlushPending();
    }
}

