using Hoho.Core.Agents;
using Hoho.Core.Guidance;
using Hoho.Core.Providers;
using Hoho.Core.Sessions;
using Terminal.Gui;

namespace Hoho;

internal static class TuiApp
{
    public static int Run(string workdir, string providerName, string? sessionId, string? initialPrompt = null)
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

        var status = new Label
        {
            X = 0,
            Y = Pos.AnchorEnd(2),
            Width = Dim.Fill(),
            Height = 1,
            Text = string.Empty
        };

        var info = new Label
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
            Text = $"Esc edit prev  |  Shift+Enter newline, Enter send  |  Provider: {provider.Name}  Workdir: {workdir}"
        };

        var composer = new ComposerView
        {
            X = 0,
            Y = Pos.AnchorEnd(3),
            Width = Dim.Fill(),
            Height = 2,
        };

        bool backtrackPrimed = false;
        void UpdateInfo()
        {
            var hints = backtrackPrimed ? "Esc edit prev (primed)" : "Esc edit prev";
            info.Text = $"{hints}  |  Shift+Enter newline, Enter send  |  Provider: {provider.Name}  Workdir: {workdir}";
        }
        UpdateInfo();

        System.Text.StringBuilder streamBuf = new();
        System.Threading.CancellationTokenSource? currentTurnCts = null;
        bool flushIdleActive = false;
        DateTime lastFlush = DateTime.MinValue;

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
            status.Text = "Running… (Ctrl-C to cancel)";
            currentTurnCts = new System.Threading.CancellationTokenSource();
            EnsureFlushIdle();
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
                status.Text = string.Empty;
                currentTurnCts?.Dispose();
                currentTurnCts = null;
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
                composer.Text = string.Empty;
                backtrackPrimed = false;
                UpdateInfo();
                return;
            }

            // Esc backtrack logic
            if (args.KeyEvent.Key == Key.Esc)
            {
                args.Handled = true;
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
                    composer.Text = string.Empty;
                    await SendAsync(prompt);
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
