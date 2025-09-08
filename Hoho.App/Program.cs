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
public static class Program {
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

            // Codex parity: root runs TUI; optional initial prompt; resume/continue flags.
            var initialPromptArg = new Argument<string?>(name: "prompt", description: "initial prompt for interactive TUI", getDefaultValue: () => null);
            var resumeOpt  = new Option<bool>(name: "--resume", description: "open recent sessions picker", getDefaultValue: () => false);
            var contOpt    = new Option<bool>(name: "--continue", description: "resume most recent session", getDefaultValue: () => false);
            var rootCommand = new RootCommand("🥋 HOHO - The CLI Agent That Just Says 'OK.'") { initialPromptArg, resumeOpt, contOpt };

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
                    "openai" => CreateOpenAIProvider(config, model),
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
                new Option<string?>("--file", description: "patch file; omit to read stdin")
            };
            patchApply.SetHandler(async (string workdir, string? file) =>
            {
                var fs = new FileService(workdir);
                var ps = new PatchService(fs);
                string text = file is { Length: > 0 } ? await File.ReadAllTextAsync(file) : await Console.In.ReadToEndAsync();
                await ps.ApplyAsync(text);
                Console.WriteLine("ok");
            }, workdirOpt, new Option<string?>("--file"));
            rootCommand.AddCommand(patchApply);

            // Repo helpers
            var repoStatus = new Command("repo-status", "git status --porcelain=v1") { workdirOpt };
            repoStatus.SetHandler(async (string workdir) =>
            {
                var gs = new GitService(new Hoho.Core.Services.ShellRunner(), workdir);
                Console.Write(await gs.StatusAsync());
            }, workdirOpt);
            rootCommand.AddCommand(repoStatus);

            var repoDiff = new Command("repo-diff", "git diff (optionally a path)") { workdirOpt, new Option<string?>("--path") };
            repoDiff.SetHandler(async (string workdir, string? path) =>
            {
                var gs = new GitService(new Hoho.Core.Services.ShellRunner(), workdir);
                Console.Write(await gs.DiffAsync(path));
            }, workdirOpt, new Option<string?>("--path"));
            rootCommand.AddCommand(repoDiff);

            var repoCommit = new Command("repo-commit", "git add -A && git commit -m <msg>") { workdirOpt, new Argument<string>("message") };
            repoCommit.SetHandler(async (string workdir, string message) =>
            {
                var gs = new GitService(new Hoho.Core.Services.ShellRunner(), workdir);
                await gs.CommitAllAsync(message);
                Console.WriteLine("ok");
            }, workdirOpt, new Argument<string>("message"));
            rootCommand.AddCommand(repoCommit);

            // Tree
            var tree = new Command("tree", "Print repo tree to given depth") { workdirOpt, new Option<int>("--depth", getDefaultValue: () => 2) };
            tree.SetHandler((string workdir, int depth) =>
            {
                foreach (var line in EnumerateTree(workdir, depth)) Console.WriteLine(line);
            }, workdirOpt, new Option<int>("--depth"));
            rootCommand.AddCommand(tree);

            // TUI default behavior
            rootCommand.SetHandler((string provider, string? sid, string workdir, string? initial, bool resume, bool cont) =>
            {
                Environment.CurrentDirectory = workdir;
                // Handle resume/continue semantics
                if (resume && cont)
                {
                    Console.Error.WriteLine("Cannot use --resume and --continue together.");
                    return;
                }
                if (resume)
                {
                    var sessions = Hoho.Core.Sessions.SessionDiscovery.ListSessions(30).ToList();
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
                            var preview = Hoho.Core.Sessions.SessionDiscovery.FirstUserPreview(sInfo.Id) ?? "(no preview)";
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
                    var sidLatest = Hoho.Core.Sessions.SessionDiscovery.ListSessions(1).FirstOrDefault()?.Id;
                    if (!string.IsNullOrWhiteSpace(sidLatest)) sid = sidLatest;
                }
                TuiApp.Run(workdir, provider, sid, initial);
            }, providerOpt, sessionOpt, workdirOpt, initialPromptArg, resumeOpt, contOpt);

            // Exec (non-interactive automation mode)
            var modelOpt = new Option<string>(name: "-m", description: "model", getDefaultValue: () => "gpt-4o-mini");
            var approvalsOpt = new Option<string>(name: "--ask-for-approval", description: "untrusted|on-failure|on-request|never", getDefaultValue: () => "on-failure");
            var sandboxOpt = new Option<string>(name: "--sandbox", description: "read-only|workspace-write|danger-full-access", getDefaultValue: () => "workspace-write");
            var exec = new Command("exec", "Non-interactive 'automation mode' like Codex") { providerOpt, modelOpt, approvalsOpt, sandboxOpt, sessionOpt, workdirOpt, promptArg };
            exec.SetHandler(async (InvocationContext ctx) =>
            {
                var providerName = ctx.ParseResult.GetValueForOption(providerOpt)!;
                var model = ctx.ParseResult.GetValueForOption(modelOpt)!;
                var appro = ctx.ParseResult.GetValueForOption(approvalsOpt)!;
                var sandbox = ctx.ParseResult.GetValueForOption(sandboxOpt)!;
                var sid = ctx.ParseResult.GetValueForOption(sessionOpt);
                var workdir = ctx.ParseResult.GetValueForOption(workdirOpt)!;
                var prompt = ctx.ParseResult.GetValueForArgument(promptArg)!;

                Environment.CurrentDirectory = workdir;

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

                var guidance = Hoho.Core.Guidance.AgentsLoader.LoadMergedAgents(workdir);
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
    public static IChatProvider CreateOpenAIProvider(Hoho.Core.Configuration.HohoConfig config, string model)
    {
        var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            config.Secrets.TryGetValue("OPENAI_API_KEY", out key);
        }
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("OPENAI_API_KEY not set in env or config");
        return new Hoho.Core.Providers.OpenAIProvider(key!, model);
    }
}
