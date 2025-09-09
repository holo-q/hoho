using System.CommandLine;
using System.CommandLine.Invocation;
using Hoho.Core.Abstractions;
using Hoho.Core.Agents;
using Hoho.Core.Configuration;
using Hoho.Core.Planning;
using Hoho.Core.Providers;
using Hoho.Core.Repo;
using Hoho.Core.Sessions;
using Hoho.Core.Tools;
using Serilog;

namespace Hoho;

/// <summary>
/// HOHO - The CLI Agent That Just Says "OK."
/// Shadow Protocol: Overwhelming power through calm, principled simplicity.
/// </summary>
public static partial class Program {
    public static async Task<int> Main(string[] args) {
		// Initialize logging
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Information()
			.MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
			.WriteTo.Console()
			.WriteTo.File("logs/hoho-.log", rollingInterval: RollingInterval.Day)
			.CreateLogger();

		try {
			Log.Information("🥋 HOHO Shadow Protocol - Startup initiated");

            // Codex parity: root runs TUI; optional initial prompt; resume/continue flags; sandbox/approvals.
            var initialPromptArg = new Argument<string?>(name: "prompt", description: "initial prompt for interactive TUI", getDefaultValue: () => null);
            var resumeOpt  = new Option<bool>(name: "--resume", description: "open recent sessions picker", getDefaultValue: () => false);
            var contOpt    = new Option<bool>(name: "--continue", description: "resume most recent session", getDefaultValue: () => false);
            var approvalsOptRoot= new Option<string>(name: "--ask-for-approval", getDefaultValue: () => "on-failure", description: "untrusted|on-failure|on-request|never");
            var sandboxOptRoot  = new Option<string>(name: "--sandbox", getDefaultValue: () => "workspace-write", description: "read-only|workspace-write|danger-full-access");
            var rootCommand = new RootCommand("🥋 HOHO - The CLI Agent That Just Says 'OK.'") { initialPromptArg, resumeOpt, contOpt, approvalsOptRoot, sandboxOptRoot };

            // Home
            var homeCommand = new Command("home", "Show hoho home screen with Saitama face");
            homeCommand.SetHandler(ShowHome);
            rootCommand.AddCommand(homeCommand);

            // Version
            var versionCommand = new Command("version", "Show hoho version");
            versionCommand.SetHandler(ShowVersion);
            rootCommand.AddCommand(versionCommand);

            // Session new
            var sessionNew = new Command("session-new", "Create a new session and print its id");
            sessionNew.SetHandler(() =>
            {
                var store = new TranscriptStore();
                var id = store.CreateNewSessionId();
                Console.WriteLine(id);
            });
            rootCommand.AddCommand(sessionNew);

            // Common options
            var providerOpt = new Option<string>(name: "--provider", getDefaultValue: () => "echo", description: "chat provider (echo|openai)");
            var modelOpt    = new Option<string>(name: "-m", description: "model", getDefaultValue: () => "gpt-4o-mini");
            var sessionOpt  = new Option<string?>(name: "--session-id", description: "existing session id (optional)");
            var workdirOpt  = new Option<string>(name: "-C", description: "working directory", getDefaultValue: () => Environment.CurrentDirectory);
            var approvalsOpt= new Option<string>(name: "--ask-for-approval", getDefaultValue: () => "on-failure", description: "untrusted|on-failure|on-request|never");
            var sandboxOpt  = new Option<string>(name: "--sandbox", getDefaultValue: () => "workspace-write", description: "read-only|workspace-write|danger-full-access");

            // Chat
            var promptArg   = new Argument<string>(name: "prompt", description: "user prompt");
            var chat = new Command("chat", "Send a prompt and stream the response") { providerOpt, modelOpt, sessionOpt, workdirOpt, promptArg };
            chat.SetHandler(async (InvocationContext ctx) =>
            {
                var providerName = ctx.ParseResult.GetValueForOption(providerOpt)!;
                var model = ctx.ParseResult.GetValueForOption(modelOpt)!;
                var sessionId = ctx.ParseResult.GetValueForOption(sessionOpt);
                var prompt = ctx.ParseResult.GetValueForArgument(promptArg)!;

                var config = HohoConfig.Load();
                var store = new TranscriptStore();
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    sessionId = store.CreateNewSessionId();
                    Console.WriteLine($"session: {sessionId}");
                }

                IChatProvider provider = providerName.ToLowerInvariant() switch
                {
                    "echo" => new EchoProvider(),
                    "openai" => ProviderFactory.CreateOpenAIProvider(config, model),
                    _ => new EchoProvider(),
                };

                var runner = new AgentRunner(provider, store);
                var buf = new System.Text.StringBuilder();
                await runner.RunOnceAsync(sessionId!, prompt, onText: s => buf.Append(s));
                Console.Write(buf.ToString());
            });
            rootCommand.AddCommand(chat);

            // Plan show
            var planShow = new Command("plan-show", "Show plan steps for a session") { sessionOpt };
            planShow.SetHandler((string? sid) =>
            {
                if (string.IsNullOrWhiteSpace(sid)) { Console.Error.WriteLine("--session-id required"); return; }
                var store = new TranscriptStore();
                var sessionDir = Path.GetDirectoryName(store.GetTranscriptPath(sid!))!;
                var svc = new PlanService(sessionDir);
                for (int i = 0; i < svc.Steps.Count; i++)
                {
                    Console.WriteLine($"{i}. {svc.Steps[i].Status}: {svc.Steps[i].Step}");
                }
            }, sessionOpt);
            rootCommand.AddCommand(planShow);

            // Plan set
            var stepsArg = new Argument<string>("steps", "e.g., 'a; b; c'");
            var planSet = new Command("plan-set", "Set plan steps from a semicolon-separated string") { sessionOpt, stepsArg };
            planSet.SetHandler((string? sid, string steps) =>
            {
                if (string.IsNullOrWhiteSpace(sid)) { Console.Error.WriteLine("--session-id required"); return; }
                var store = new TranscriptStore();
                var sessionDir = Path.GetDirectoryName(store.GetTranscriptPath(sid!))!;
                var svc = new PlanService(sessionDir);
                var list = steps.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).Select(s => new PlanStep(s, PlanStatus.Pending));
                svc.SetSteps(list);
                Console.WriteLine("ok");
            }, sessionOpt, stepsArg);
            rootCommand.AddCommand(planSet);

            // Plan update
            var planUpdate = new Command("plan-update", "Update a step status")
            {
                sessionOpt,
                new Argument<int>("index"),
                new Argument<string>("status", description: "Pending|InProgress|Completed"),
            };
            planUpdate.SetHandler((string? sid, int idx, string status) =>
            {
                if (string.IsNullOrWhiteSpace(sid)) { Console.Error.WriteLine("--session-id required"); return; }
                var store = new TranscriptStore();
                var sessionDir = Path.GetDirectoryName(store.GetTranscriptPath(sid!))!;
                var svc = new PlanService(sessionDir);
                var s = Enum.Parse<PlanStatus>(status, ignoreCase: true);
                svc.UpdateStep(idx, svc.Steps[idx] with { Status = s });
                Console.WriteLine("ok");
            }, sessionOpt, new Argument<int>("index"), new Argument<string>("status"));
            rootCommand.AddCommand(planUpdate);

            // Patch apply
            var patchApply = new Command("patch-apply", "Apply a simplified apply_patch envelope")
            {
                workdirOpt,
                approvalsOpt,
                sandboxOpt,
                new Option<string?>("--file", description: "patch file; omit to read stdin")
            };
            patchApply.SetHandler(async (string workdir, string approvals, string sandbox, string? file) =>
            {
                var mode = sandbox.ToLowerInvariant() switch
                {
                    "read-only" => Core.Sandbox.SandboxMode.ReadOnly,
                    "danger-full-access" => Core.Sandbox.SandboxMode.DangerFullAccess,
                    _ => Core.Sandbox.SandboxMode.WorkspaceWrite,
                };
                var fs = new FileService(workdir, mode);
                var ps = new PatchService(fs);
                string text = file is { Length: > 0 } ? await File.ReadAllTextAsync(file) : await Console.In.ReadToEndAsync();
                var ap = ParseApprovals(approvals);
                var gate = new Core.Approvals.ApprovalService(ap);
                if (!gate.Confirm("Apply patch to workspace")) { Console.WriteLine("canceled"); return; }
                var result = await ps.ApplyAsync(text);
                if (result.Changes.Count == 0) Console.WriteLine("No changes");
                else Console.WriteLine(result.ToString());
                // Auto-run fixers for C# after patch apply
                await RunFixAsync(workdir);
            }, workdirOpt, approvalsOpt, sandboxOpt, new Option<string?>("--file"));
            rootCommand.AddCommand(patchApply);

            // Fix: organize imports and add GlobalUsings templates if missing
            var fix = new Command("fix", "Organize imports and add GlobalUsings templates (C#)") { workdirOpt };
            fix.SetHandler(async (string workdir) =>
            {
                await RunFixAsync(workdir);
            }, workdirOpt);
            rootCommand.AddCommand(fix);

            // Repo helpers
            var repoStatus = new Command("repo-status", "git status --porcelain=v1") { workdirOpt };
            repoStatus.SetHandler(async (string workdir) =>
            {
                var gs = new GitService(new Core.Services.ShellRunner(), workdir);
                Console.Write(await gs.StatusAsync());
            }, workdirOpt);
            rootCommand.AddCommand(repoStatus);

            var repoDiff = new Command("repo-diff", "git diff (optionally a path)") { workdirOpt, new Option<string?>("--path") };
            repoDiff.SetHandler(async (string workdir, string? path) =>
            {
                var gs = new GitService(new Core.Services.ShellRunner(), workdir);
                Console.Write(await gs.DiffAsync(path));
            }, workdirOpt, new Option<string?>("--path"));
            rootCommand.AddCommand(repoDiff);

            var repoCommit = new Command("repo-commit", "git add -A && git commit -m <msg>") { workdirOpt, approvalsOpt, new Argument<string>("message") };
            repoCommit.SetHandler(async (string workdir, string approvals, string message) =>
            {
                var gs = new GitService(new Core.Services.ShellRunner(), workdir);
                var ap = ParseApprovals(approvals);
                var gate = new Core.Approvals.ApprovalService(ap);
                if (!gate.Confirm("Commit all changes")) { Console.WriteLine("canceled"); return; }
                await gs.CommitAllAsync(message);
                Console.WriteLine("ok");
            }, workdirOpt, approvalsOpt, new Argument<string>("message"));
            rootCommand.AddCommand(repoCommit);

            // Tree
            var tree = new Command("tree", "Print repo tree to given depth") { workdirOpt, new Option<int>("--depth", getDefaultValue: () => 2) };
            tree.SetHandler((string workdir, int depth) =>
            {
                foreach (var line in EnumerateTree(workdir, depth)) Console.WriteLine(line);
            }, workdirOpt, new Option<int>("--depth"));
            rootCommand.AddCommand(tree);

            // TUI default behavior
            var smokeOpt = new Option<int?>(name: "--tui-smoke-ms", description: "Run TUI for N ms then exit (smoke test)");
            var backendOpt = new Option<string>(name: "--backend", getDefaultValue: () => "terminal-gui", description: "TUI backend: terminal-gui|ratatui");
            rootCommand.AddOption(smokeOpt);
            rootCommand.AddOption(backendOpt);
            rootCommand.SetHandler((string provider, string? sid, string? workdir, string? initial, bool resume, bool cont, int? smokeMs, string backendName) =>
            {
                // Normalize working directory; default to current if missing
                if (string.IsNullOrWhiteSpace(workdir))
                {
                    workdir = Directory.GetCurrentDirectory();
                }
                else if (!Path.IsPathRooted(workdir))
                {
                    workdir = Path.GetFullPath(workdir);
                }
                try { Environment.CurrentDirectory = workdir; }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"error: invalid workdir '{workdir}': {ex.Message}");
                    return;
                }
                var cfgRoot = HohoConfig.Load();
                // Handle resume/continue semantics
                if (resume && cont)
                {
                    Console.Error.WriteLine("Cannot use --resume and --continue together.");
                    return;
                }
                // Enforce parity: unless ExperimentalUi is enabled, reject unsupported flags
                if (!cfgRoot.ExperimentalUi && (resume || cont))
                {
                    if (resume) { Console.Error.WriteLine("error: unexpected argument '--resume' found"); }
                    if (cont)   { Console.Error.WriteLine("error: unexpected argument '--continue' found"); }
                    return;
                }
                if (resume)
                {
                    var sessions = SessionDiscovery.ListSessions(30).ToList();
                    if (sessions.Count == 0)
                    {
                        Console.WriteLine("No sessions found.");
                    }
                    else
                    {
                        Console.WriteLine("Recent sessions:");
                        for (int i = 0; i < sessions.Count; i++)
                        {
                            var sInfo = sessions[i];
                            var preview = SessionDiscovery.FirstUserPreview(sInfo.Id) ?? "(no preview)";
                            Console.WriteLine($"{i + 1,2}. {sInfo.Id}  -  {preview}");
                        }
                        Console.Write("Select (1-" + sessions.Count + "): ");
                        var choice = Console.ReadLine();
                        if (int.TryParse(choice, out var idx) && idx >= 1 && idx <= sessions.Count)
                        {
                            sid = sessions[idx - 1].Id;
                        }
                    }
                }
                else if (cont)
                {
                    var sidLatest = SessionDiscovery.ListSessions(1).FirstOrDefault()?.Id;
                    if (!string.IsNullOrWhiteSpace(sidLatest)) sid = sidLatest;
                }
                ITuiBackend backend = backendName.ToLowerInvariant() switch
                {
                    "terminal-gui" => new TerminalGuiBackend(),
                    "ratatui" => new NotImplementedBackend(),
                    _ => new TerminalGuiBackend(),
                };
                backend.Run(workdir!, provider, sid, initial, smokeMs);
            }, providerOpt, sessionOpt, workdirOpt, initialPromptArg, resumeOpt, contOpt, smokeOpt, backendOpt);

            // Exec (non-interactive automation mode)
            var execModelOpt = new Option<string>(name: "-m", description: "model", getDefaultValue: () => "gpt-4o-mini");
            var execApprovalsOpt = new Option<string>(name: "--ask-for-approval", description: "untrusted|on-failure|on-request|never", getDefaultValue: () => "on-failure");
            var execSandboxOpt = new Option<string>(name: "--sandbox", description: "read-only|workspace-write|danger-full-access", getDefaultValue: () => "workspace-write");
            var exec = new Command("exec", "Non-interactive 'automation mode' like Codex") { providerOpt, execModelOpt, execApprovalsOpt, execSandboxOpt, sessionOpt, workdirOpt, promptArg };
            exec.SetHandler(async (InvocationContext ctx) =>
            {
                var providerName = ctx.ParseResult.GetValueForOption(providerOpt)!;
                var model = ctx.ParseResult.GetValueForOption(execModelOpt)!;
                var appro = ctx.ParseResult.GetValueForOption(execApprovalsOpt)!;
                var sandbox = ctx.ParseResult.GetValueForOption(execSandboxOpt)!;
                var sid = ctx.ParseResult.GetValueForOption(sessionOpt);
                var workdir = ctx.ParseResult.GetValueForOption(workdirOpt);
                var prompt = ctx.ParseResult.GetValueForArgument(promptArg)!;

                // Normalize working directory
                if (string.IsNullOrWhiteSpace(workdir))
                {
                    workdir = Directory.GetCurrentDirectory();
                }
                else if (!Path.IsPathRooted(workdir))
                {
                    workdir = Path.GetFullPath(workdir);
                }
                try { Environment.CurrentDirectory = workdir; }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"error: invalid workdir '{workdir}': {ex.Message}");
                    return;
                }

                var config = HohoConfig.Load();
                var store = new TranscriptStore();
                if (string.IsNullOrWhiteSpace(sid))
                {
                    sid = store.CreateNewSessionId();
                    Console.WriteLine($"session: {sid}");
                }

                IChatProvider provider = providerName.ToLowerInvariant() switch
                {
                    "echo" => new EchoProvider(),
                    "openai" => ProviderFactory.CreateOpenAIProvider(config, model),
                    _ => new EchoProvider(),
                };

                var guidance = Core.Guidance.AgentsLoader.LoadMergedAgents(workdir);
                var runner = new AgentRunner(provider, store);
                var buf = new System.Text.StringBuilder();
                await runner.RunOnceAsync(sid!, prompt, onText: s => buf.Append(s), systemPrompt: guidance);
                Console.Write(buf.ToString());
            });
            rootCommand.AddCommand(exec);

        return await rootCommand.InvokeAsync(args);
    } catch (Exception ex) {
        Log.Error(ex, "Fatal error in HOHO Shadow Protocol");
        return 1;
    } finally {
        Log.Information("🥋 HOHO Shadow Protocol - Shutdown complete");
        Log.CloseAndFlush();
    }
}

    private static Core.Sandbox.ApprovalPolicy ParseApprovals(string s)
    {
        return s.ToLowerInvariant() switch
        {
            "never" => Core.Sandbox.ApprovalPolicy.Never,
            "on-request" => Core.Sandbox.ApprovalPolicy.OnRequest,
            "untrusted" => Core.Sandbox.ApprovalPolicy.Untrusted,
            _ => Core.Sandbox.ApprovalPolicy.OnFailure,
        };
    }

    private static Core.Sandbox.SandboxMode ParseSandbox(string s)
    {
        return s.ToLowerInvariant() switch
        {
            "read-only" => Core.Sandbox.SandboxMode.ReadOnly,
            "danger-full-access" => Core.Sandbox.SandboxMode.DangerFullAccess,
            _ => Core.Sandbox.SandboxMode.WorkspaceWrite,
        };
    }

	private static void ShowHome() {
		Console.WriteLine(ReadEmbedded("art_2.txt"));
		Console.WriteLine();
		Console.WriteLine("HOHO - The CLI Agent That Just Says 'OK.'");
		Console.WriteLine("Shadow Protocol Active");
	}

    private static string ReadEmbedded(string name)
    {
        var asm = typeof(Program).Assembly;
        var resource = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));
        if (resource is null) return "";
        using var s = asm.GetManifestResourceStream(resource);
        if (s is null) return "";
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    private static void ShowVersion() {
        Console.WriteLine("hoho v0.1.0 - Native C# Shadow Protocol");
    }

    private static IEnumerable<string> EnumerateTree(string root, int depth)
    {
        var rootFull = Path.GetFullPath(root);
        var stack = new Stack<(DirectoryInfo dir, int d)>();
        stack.Push((new DirectoryInfo(rootFull), 0));
        yield return $"{rootFull}";
        while (stack.Count > 0)
        {
            var (dir, d) = stack.Pop();
            if (d >= depth) continue;
            IEnumerable<FileSystemInfo> entries;
            try { entries = dir.EnumerateFileSystemInfos(); } catch { continue; }
            foreach (var e in entries.OrderBy(e => e is DirectoryInfo ? 0 : 1).ThenBy(e => e.Name))
            {
                var rel = Path.GetRelativePath(rootFull, e.FullName);
                yield return rel.Replace('\\', '/');
                if (e is DirectoryInfo di)
                {
                    stack.Push((di, d + 1));
                }
            }
        }
    }
}

static class ProviderFactory
{
    public static IChatProvider CreateOpenAIProvider(HohoConfig config, string model)
    {
        var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            config.Secrets.TryGetValue("OPENAI_API_KEY", out key);
        }
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("OPENAI_API_KEY not set in env or config");
        return new OpenAIProvider(key!, model);
    }
}

static class Fixers
{
    public static void EnsureGlobalUsings(string workdir)
    {
        // Hoho.App global usings
        var appUsingsPath = Path.Combine(workdir, "Hoho.App", "GlobalUsings.cs");
        if (!File.Exists(appUsingsPath))
        {
            var content = string.Join('\n', new []
            {
                "global using Terminal.Gui;",
                "global using Terminal.Gui.Views;",
                "global using Terminal.Gui.ViewBase;",
                "global using Terminal.Gui.Input;",
                "global using System.Text;",
                ""
            });
            try { File.WriteAllText(appUsingsPath, content); } catch { /* ignore */ }
        }
        // Hoho.Core global usings
        var coreUsingsPath = Path.Combine(workdir, "Hoho.Core", "GlobalUsings.cs");
        if (!File.Exists(coreUsingsPath))
        {
            var content = string.Join('\n', new []
            {
                "global using Hoho.Core.Sessions;",
                ""
            });
            try { File.WriteAllText(coreUsingsPath, content); } catch { /* ignore */ }
        }
    }
}

partial class Program
{
    private static async Task RunFixAsync(string workdir)
    {
        Fixers.EnsureGlobalUsings(workdir);
        // Try running dotnet format if available
        try
        {
            var shell = new Core.Services.ShellRunner();
            await foreach (var c in shell.RunAsync(new []{"dotnet","format","Hoho.sln","--severity","info"}, new ShellOptions{ WorkDir = workdir, AllowedRoot = workdir, AllowNetwork = false }))
            {
                if (c.Stream == "stdout") Console.Write(c.Data);
            }
        }
        catch { /* ignore if not available */ }
        Console.WriteLine("fix: completed");
    }
}
