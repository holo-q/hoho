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
        var inHunks = false;
        List<string>? baseLines = null;
        var hunkLines = new List<string>();

        async Task FlushPending()
        {
            if (op is null || path is null) return;
            switch (op)
            {
                case "Add File":
                    await _files.WriteTextAsync(path, buffer.ToString());
                    break;
                case "Update File":
                    if (moveTo is not null)
                    {
                        _files.Move(path, moveTo);
                        path = moveTo;
                    }
                    if (inHunks)
                    {
                        baseLines ??= new List<string>((await _files.ReadTextAsync(path)).Split('\n'));
                        var result = ApplyHunks(baseLines!, hunkLines);
                        await _files.WriteTextAsync(path, string.Join("\n", result));
                    }
                    else
                    {
                        await _files.WriteTextAsync(path, buffer.ToString());
                    }
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
                await FlushPending();
                op = "Add File";
                path = line.Substring("*** Add File: ".Length).Trim();
                moveTo = null;
                buffer.Clear();
                baseLines = null; inHunks = false; hunkLines.Clear();
                continue;
            }
            if (line.StartsWith("*** Update File: "))
            {
                await FlushPending();
                op = "Update File";
                path = line.Substring("*** Update File: ".Length).Trim();
                moveTo = null;
                buffer.Clear();
                baseLines = null; inHunks = false; hunkLines.Clear();
                continue;
            }
            if (line.StartsWith("*** Delete File: "))
            {
                await FlushPending();
                op = "Delete File";
                path = line.Substring("*** Delete File: ".Length).Trim();
                moveTo = null;
                buffer.Clear();
                baseLines = null; inHunks = false; hunkLines.Clear();
                continue;
            }
            if (line.StartsWith("*** Move to: "))
            {
                moveTo = line.Substring("*** Move to: ".Length).Trim();
                continue;
            }
            if (op == "Add File" || op == "Update File")
            {
                if (line.StartsWith("@@"))
                {
                    // Start of hunks mode for Update File
                    inHunks = true;
                    if (op == "Add File")
                        throw new NotSupportedException("Hunks not supported for Add File");
                    hunkLines.Add(line);
                    continue;
                }
                if (inHunks)
                {
                    hunkLines.Add(line);
                    continue;
                }
                if (line.Length > 0 && (line[0] == '+' || line[0] == ' '))
                {
                    // Treat both '+' and ' ' as content lines for full-replace mode
                    buffer.AppendLine(line.Substring(1));
                    continue;
                }
            }
        }

        await FlushPending();
    }

    private static List<string> ApplyHunks(List<string> baseLines, List<string> patch)
    {
        // Very simple unified-hunk applier using +, -, and space lines; ignores positions in @@
        var output = new List<string>();
        int idx = 0;
        for (int i = 0; i < patch.Count; i++)
        {
            var ln = patch[i];
            if (ln.StartsWith("@@"))
            {
                // Hunk header; nothing to do
                continue;
            }
            if (ln.Length == 0) continue;
            var tag = ln[0];
            var content = ln.Length > 1 ? ln.Substring(1) : string.Empty;
            switch (tag)
            {
                case ' ':
                    // context: must match base
                    if (idx >= baseLines.Count || baseLines[idx] != content)
                        throw new InvalidOperationException("Patch context mismatch");
                    output.Add(baseLines[idx]);
                    idx++;
                    break;
                case '-':
                    // removal: base must match; skip it
                    if (idx >= baseLines.Count || baseLines[idx] != content)
                        throw new InvalidOperationException("Patch removal mismatch");
                    idx++;
                    break;
                case '+':
                    // insertion
                    output.Add(content);
                    break;
                default:
                    // treat as passthrough
                    output.Add(ln);
                    break;
            }
        }
        // Append remaining base lines if any
        for (; idx < baseLines.Count; idx++) output.Add(baseLines[idx]);
        return output;
    }
}
