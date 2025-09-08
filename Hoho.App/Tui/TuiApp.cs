using Hoho.Core.Agents;
using Hoho.Core.Guidance;
using Hoho.Core.Providers;
using Hoho.Core.Sessions;
using Terminal.Gui;

namespace Hoho;

internal static class TuiApp
{
    public static int Run(string workdir, string providerName, string? sessionId, string? initialPrompt = null, Hoho.Core.Sandbox.ApprovalPolicy approval = Hoho.Core.Sandbox.ApprovalPolicy.OnFailure, Hoho.Core.Sandbox.SandboxMode sandbox = Hoho.Core.Sandbox.SandboxMode.WorkspaceWrite, bool experimentalUi = false)
    {
        Application.Init();

        var store = new TranscriptStore();
        var sid = sessionId;
        if (string.IsNullOrWhiteSpace(sid)) sid = store.CreateNewSessionId();

        IChatProvider provider = providerName.ToLowerInvariant() switch
        {
            "echo" => new EchoProvider(),
            _ => new EchoProvider(),
        };
        var runner = new AgentRunner(provider, store);
        var system = AgentsLoader.LoadMergedAgents(workdir);

        var top = Application.Top;

        // Single column chat list + composer + status + info line
        var chat = new ChatView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(3)
        };

        var status = new StatusIndicatorView
        {
            X = 0,
            Y = Pos.AnchorEnd(2),
            Width = Dim.Fill(),
            Height = 1,
        };

        var info = new Label
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
            Text = $"Esc edit prev  |  Shift+Enter newline, Enter send  |  Provider: {provider.Name}  Workdir: {workdir}"
        };
        // Cyan tips like Codex
        info.ColorScheme = new ColorScheme
        {
            Normal = Application.Driver.MakeAttribute(Color.Cyan, Color.Black),
        };

        var composer = new ComposerView
        {
            X = 0,
            Y = Pos.AnchorEnd(3),
            Width = Dim.Fill(),
            Height = 2,
        };

        void UpdateLayout()
        {
            var h = status.DesiredHeight;
            status.Height = h;
            composer.Y = Pos.AnchorEnd(1 + h);
            status.Y = Pos.AnchorEnd(1 + h);
            chat.Height = Dim.Fill(1 + h + composer.Height);
        }
        UpdateLayout();

        bool backtrackPrimed = false;
        void UpdateInfo()
        {
            var hints = backtrackPrimed ? "Esc edit prev (primed)" : "Esc edit prev";
            var common = $"{hints} | Shift+Enter newline; Enter send";
            var extra = experimentalUi ? " | @ insert path; Ctrl+K files" : string.Empty;
            info.Text = $"{common}{extra} | Mode: {sandbox} | Approvals: {approval} | Provider: {provider.Name} | {workdir}";
        }
        UpdateInfo();

        System.Text.StringBuilder streamBuf = new();
        System.Threading.CancellationTokenSource? currentTurnCts = null;
        bool flushIdleActive = false;
        DateTime lastFlush = DateTime.MinValue;
        const bool streamUi = false; // match Codex calm UX: show final output only
        var promptQueue = new System.Collections.Generic.Queue<string>();
        bool drainingQueue = false;
        var userHistory = new System.Collections.Generic.List<string>();
        var persistentHistory = new Hoho.Core.Sessions.HistoryStore().LoadRecent(200).ToList();
        int historyIndex = 0; // points to next insert position
        string? lastHistoryText = null;

        void EnsureFlushIdle()
        {
            if (flushIdleActive) return;
            flushIdleActive = true;
            lastFlush = DateTime.UtcNow;
            Application.MainLoop.AddIdle(() =>
            {
                // Called frequently on UI thread
                if (currentTurnCts is null)
                {
                    flushIdleActive = false;
                    return false; // stop idle callback
                }
                var now = DateTime.UtcNow;
                if (streamBuf.Length > 0 && (now - lastFlush).TotalMilliseconds >= 33)
                {
                    var s = streamBuf.ToString();
                    streamBuf.Clear();
                    chat.AppendAssistantChunk(s);
                    lastFlush = now;
                }
                return true; // keep callback
            });
        }

        async Task FlushStreamAsync()
        {
            if (streamBuf.Length == 0) return;
            var s = streamBuf.ToString();
            streamBuf.Clear();
            Application.MainLoop?.Invoke(() => chat.AppendAssistantChunk(s));
        }

        async Task SendAsync(string prompt)
        {
            chat.AppendUser(prompt);
            chat.BeginAssistant();
            status.Start();
            status.ColorScheme = new ColorScheme { Normal = Application.Driver.MakeAttribute(Color.Gray, Color.Black) };
            currentTurnCts = new System.Threading.CancellationTokenSource();
            if (streamUi) EnsureFlushIdle();
            try
            {
                await runner.RunOnceAsync(sid!, prompt, onText: s =>
                {
                    // Buffer streaming tokens to reduce redraw churn
                    streamBuf.Append(s);
                }, systemPrompt: system, ct: currentTurnCts.Token);
                await FlushStreamAsync();
            }
            catch (OperationCanceledException)
            {
                await FlushStreamAsync();
                chat.AppendInfo("🖐  Turn interrupted");
            }
            catch (Exception ex)
            {
                chat.AppendInfo($"error: {ex.Message}");
            }
            finally
            {
                chat.EndAssistant();
                status.Stop();
                currentTurnCts?.Dispose();
                currentTurnCts = null;
                // Drain queued prompts
                if (!drainingQueue && promptQueue.Count > 0)
                {
                    drainingQueue = true;
                    while (promptQueue.Count > 0)
                    {
                        var next = promptQueue.Dequeue();
                        status.QueuedCount = promptQueue.Count;
                        status.QueuedPreviews = promptQueue.Take(2).ToArray();
                        UpdateLayout();
                        await SendAsync(next);
                    }
                    drainingQueue = false;
                    status.QueuedPreviews = Array.Empty<string>();
                    status.QueuedCount = 0;
                    UpdateLayout();
                }
            }
        }

        composer.KeyPressInner += async args =>
        {
            // Ctrl-C clears composer
            if (args.KeyEvent.Key == Key.C && (args.KeyEvent.KeyModifiers & KeyModifiers.Ctrl) != 0)
            {
                args.Handled = true;
                if (currentTurnCts is not null)
                {
                    currentTurnCts.Cancel();
                }
                composer.Clear();
                backtrackPrimed = false;
                UpdateInfo();
                return;
            }

            // Esc to interrupt if running (and not in paste burst)
            if (args.KeyEvent.Key == Key.Esc && currentTurnCts is not null)
            {
                if (composer.IsInPasteBurst) { args.Handled = true; return; }
                args.Handled = true;
                currentTurnCts.Cancel();
                return;
            }

            // Esc backtrack logic (suppress during paste burst)
            if (args.KeyEvent.Key == Key.Esc)
            {
                args.Handled = true;
                if (composer.IsInPasteBurst) return;
                var txt = composer.Text;
                if (string.IsNullOrEmpty(txt))
                {
                    if (!backtrackPrimed)
                    {
                        backtrackPrimed = true;
                    }
                    else
                    {
                        var last = chat.GetLastUserMessageText();
                        var preview = new BacktrackPreview(last);
                        preview.Accepted += () =>
                        {
                            composer.Text = last ?? string.Empty;
                            backtrackPrimed = false;
                            UpdateInfo();
                            Application.RequestStop();
                        };
                        preview.Canceled += () =>
                        {
                            backtrackPrimed = false;
                            UpdateInfo();
                            Application.RequestStop();
                        };
                        Application.Run(preview);
                    }
                    UpdateInfo();
                }
                return;
            }

            // '@' file search trigger (ignore during paste-burst)
            if (!composer.IsInPasteBurst && (uint)args.KeyEvent.Key <= 0x7E && (char)args.KeyEvent.Key == '@' && args.KeyEvent.KeyModifiers == 0)
            {
                args.Handled = true;
                var sel = FileSearchPopup.Show(workdir);
                if (!string.IsNullOrEmpty(sel))
                {
                    composer.InsertText(sel);
                }
                else
                {
                    composer.InsertText("@");
                }
                return;
            }

            // ALT+UP/DOWN: navigate history (previous user prompts)
            if ((args.KeyEvent.KeyModifiers & KeyModifiers.Alt) != 0 && (args.KeyEvent.Key == Key.CursorUp || args.KeyEvent.Key == Key.CursorDown))
            {
                args.Handled = true;
                // Build combined history: persistent first (oldest..newest), then session-local
                var combined = new System.Collections.Generic.List<string>(persistentHistory);
                combined.AddRange(userHistory);
                if (combined.Count == 0) return;
                if (args.KeyEvent.Key == Key.CursorUp)
                {
                    historyIndex = System.Math.Max(0, historyIndex - 1);
                }
                else
                {
                    historyIndex = System.Math.Min(combined.Count - 1, historyIndex + 1);
                }
                composer.Text = combined[historyIndex];
                lastHistoryText = composer.Text;
                return;
            }

            // UP/DOWN: only navigate history when caret at start (approximate) and text equals last recall or empty
            if (args.KeyEvent.KeyModifiers == 0 && (args.KeyEvent.Key == Key.CursorUp || args.KeyEvent.Key == Key.CursorDown))
            {
                var textNow = composer.Text ?? string.Empty;
                if (string.IsNullOrEmpty(textNow) || (!string.IsNullOrEmpty(lastHistoryText) && textNow == lastHistoryText))
                {
                    args.Handled = true;
                    var combined = new System.Collections.Generic.List<string>(persistentHistory);
                    combined.AddRange(userHistory);
                    if (combined.Count == 0) return;
                    if (args.KeyEvent.Key == Key.CursorUp)
                        historyIndex = System.Math.Max(0, historyIndex - 1);
                    else
                        historyIndex = System.Math.Min(combined.Count - 1, historyIndex + 1);
                    composer.Text = combined[historyIndex];
                    lastHistoryText = composer.Text;
                    return;
                }
            }

            if (args.KeyEvent.Key == Key.Enter)
            {
                args.Handled = true;
                if ((args.KeyEvent.KeyModifiers & KeyModifiers.Shift) != 0)
                {
                    composer.InsertNewLine();
                }
                else
                {
                    var prompt = composer.Text;
                    composer.Clear();
                    if (currentTurnCts is not null)
                    {
                        promptQueue.Enqueue(prompt);
                        status.QueuedCount = promptQueue.Count;
                        status.QueuedPreviews = promptQueue.Take(2).ToArray();
                        UpdateLayout();
                    }
                    else
                    {
                        // Append to local and persistent history (avoid consecutive dupes)
                        if (userHistory.Count == 0 || userHistory[^1] != prompt)
                        {
                            userHistory.Add(prompt);
                        }
                        if (persistentHistory.Count == 0 || persistentHistory[^1] != prompt)
                        {
                            new Hoho.Core.Sessions.HistoryStore().Append(prompt);
                            persistentHistory.Add(prompt);
                        }
                        historyIndex = userHistory.Count - 1;
                        await SendAsync(prompt);
                    }
                }
                return;
            }

            // Ctrl+K opens file search popup and inserts selected path
            if (args.KeyEvent.Key == Key.K && (args.KeyEvent.KeyModifiers & KeyModifiers.Ctrl) != 0)
            {
                args.Handled = true;
                var sel = FileSearchPopup.Show(workdir);
                if (!string.IsNullOrEmpty(sel))
                {
                    var t = composer.Text;
                    composer.Text = (t.Length == 0 ? sel : t + sel);
                }
                return;
            }
        };

        top.Add(chat, composer, status, info);

        // Ctrl+R to open resume picker (switch session); Ctrl+P apply patch dialog
        top.KeyPress += e =>
        {
            if (experimentalUi && e.KeyEvent.Key == Key.R && (e.KeyEvent.KeyModifiers & KeyModifiers.Ctrl) != 0)
            {
                e.Handled = true;
                var pick = ResumePicker.Show();
                if (!string.IsNullOrWhiteSpace(pick))
                {
                    sid = pick;
                    chat.AppendInfo($"[resumed session: {sid}]");
                }
            }
            else if (experimentalUi && e.KeyEvent.Key == Key.P && (e.KeyEvent.KeyModifiers & KeyModifiers.Ctrl) != 0)
            {
                e.Handled = true;
                var dlg = new ApplyPatchDialog(workdir, sandbox, approval);
                Application.Run(dlg);
            }
            else if (experimentalUi && e.KeyEvent.Key == Key.G && (e.KeyEvent.KeyModifiers & KeyModifiers.Ctrl) != 0)
            {
                e.Handled = true;
                var dlg = new CommitDialog(workdir, approval);
                Application.Run(dlg);
            }
            else if (experimentalUi && e.KeyEvent.Key == Key.M && (e.KeyEvent.KeyModifiers & KeyModifiers.Ctrl) != 0)
            {
                e.Handled = true;
                // Cycle sandbox mode
                sandbox = sandbox switch
                {
                    Hoho.Core.Sandbox.SandboxMode.ReadOnly => Hoho.Core.Sandbox.SandboxMode.WorkspaceWrite,
                    Hoho.Core.Sandbox.SandboxMode.WorkspaceWrite => Hoho.Core.Sandbox.SandboxMode.DangerFullAccess,
                    _ => Hoho.Core.Sandbox.SandboxMode.ReadOnly,
                };
                UpdateInfo();
            }
            else if (experimentalUi && e.KeyEvent.Key == Key.A && (e.KeyEvent.KeyModifiers & KeyModifiers.Ctrl) != 0)
            {
                e.Handled = true;
                // Cycle approval policy
                approval = approval switch
                {
                    Hoho.Core.Sandbox.ApprovalPolicy.Never => Hoho.Core.Sandbox.ApprovalPolicy.OnFailure,
                    Hoho.Core.Sandbox.ApprovalPolicy.OnFailure => Hoho.Core.Sandbox.ApprovalPolicy.OnRequest,
                    Hoho.Core.Sandbox.ApprovalPolicy.OnRequest => Hoho.Core.Sandbox.ApprovalPolicy.Untrusted,
                    _ => Hoho.Core.Sandbox.ApprovalPolicy.Never,
                };
                UpdateInfo();
            }
        };

        if (!string.IsNullOrWhiteSpace(initialPrompt))
        {
            Application.MainLoop.AddIdle(async () =>
            {
                await SendAsync(initialPrompt!);
                return false;
            });
        }

        Application.Run();
        Application.Shutdown();
        return 0;
    }
}
