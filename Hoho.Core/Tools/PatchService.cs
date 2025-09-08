using System.Text;

namespace Hoho.Core.Tools;

public sealed class PatchService
{
    private readonly FileService _files;
    public PatchService(FileService files) { _files = files; }

    public sealed record PatchChange(string Path, string Operation, int Added, int Removed);
    public sealed record PatchApplyResult(System.Collections.Generic.IReadOnlyList<PatchChange> Changes)
    {
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in Changes) sb.AppendLine("- "+c.Operation+": "+c.Path+" (+"+c.Added+" -"+c.Removed+")");
            return sb.ToString();
        }
    }

    public async Task<PatchApplyResult> ApplyAsync(string patchText, CancellationToken ct = default)
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
        var changes = new List<PatchChange>();

        async Task FlushPending()
        {
            if (op is null || path is null) return;
            switch (op)
            {
                case "Add File":
                    var addContent = buffer.ToString();
                    await _files.WriteTextAsync(path, addContent);
                    changes.Add(new PatchChange(path, "add", CountLines(addContent), 0));
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
                        var (result, added, removed) = ApplyHunks(baseLines!, hunkLines);
                        await _files.WriteTextAsync(path, string.Join("\n", result));
                        changes.Add(new PatchChange(path, "update", added, removed));
                    }
                    else
                    {
                        var prev = await _files.ReadTextAsync(path);
                        var next = buffer.ToString();
                        await _files.WriteTextAsync(path, next);
                        changes.Add(new PatchChange(path, "update", CountLines(next), CountLines(prev)));
                    }
                    break;
                case "Delete File":
                    var prior = await _files.ReadTextAsync(path);
                    _files.Delete(path);
                    changes.Add(new PatchChange(path, "delete", 0, CountLines(prior)));
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
        return new PatchApplyResult(changes);
    }

    private static int CountLines(string s) { if (string.IsNullOrEmpty(s)) return 0; return s.Split('\n').Length; }

    private static (System.Collections.Generic.List<string> result, int added, int removed) ApplyHunks(System.Collections.Generic.List<string> baseLines, System.Collections.Generic.List<string> patch)
    {
        // Very simple unified-hunk applier using +, -, and space lines; ignores positions in @@
        var output = new System.Collections.Generic.List<string>();
        int idx = 0; int add = 0, rem = 0;
        // Helper: try to resynchronize idx to next occurrence of a context line within a small window
        int FindNextIndex(string ctx, int start, int window = 80)
        {
            int end = System.Math.Min(baseLines.Count, start + window);
            for (int j = start; j < end; j++)
                if (Norm(baseLines[j]) == ctx) return j;
            return -1;
        }
        string Norm(string s) => s.Replace("\r", string.Empty);

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
                    if (idx >= baseLines.Count || Norm(baseLines[idx]) != Norm(content))
                    {
                        // Attempt to resync to next matching context line
                        var next = FindNextIndex(Norm(content), idx);
                        if (next < 0) throw new InvalidOperationException("Patch context mismatch");
                        // Append untouched lines to output to catch up
                        for (; idx < next; idx++) output.Add(baseLines[idx]);
                    }
                    output.Add(baseLines[idx]);
                    idx++;
                    break;
                case '-':
                    // removal: base must match; skip it
                    if (idx >= baseLines.Count || Norm(baseLines[idx]) != Norm(content))
                    {
                        var next = FindNextIndex(Norm(content), idx);
                        if (next < 0) throw new InvalidOperationException("Patch removal mismatch");
                        // Append intervening lines untouched, then remove
                        for (; idx < next; idx++) output.Add(baseLines[idx]);
                    }
                    idx++;
                    rem++;
                    break;
                case '+':
                    // insertion
                    output.Add(content);
                    add++;
                    break;
                default:
                    // treat as passthrough
                    output.Add(ln);
                    break;
            }
        }
        // Append remaining base lines if any
        for (; idx < baseLines.Count; idx++) output.Add(baseLines[idx]);
        return (output, add, rem);
    }
}
