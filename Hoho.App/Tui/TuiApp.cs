using Hoho.Core.Agents;
using Hoho.Core.Guidance;
using Hoho.Core.Providers;
using Hoho.Core.Sessions;
using Terminal.Gui;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Terminal.Gui.App;

namespace Hoho;

internal static class TuiApp
{
	public static int Run(string workdir, string providerName, string? sessionId, string? initialPrompt = null)
	{
		Application.Init();

		var store                               = new TranscriptStore();
		var sid                                 = sessionId;
		if (string.IsNullOrWhiteSpace(sid)) sid = store.CreateNewSessionId();

		IChatProvider provider = providerName.ToLowerInvariant() switch
		{
			"echo" => new EchoProvider(),
			_      => new EchoProvider(),
		};
		var runner = new AgentRunner(provider, store);
		var system = AgentsLoader.LoadMergedAgents(workdir);

		var top = Application.Top;

		// Single column chat list + composer + status + info line
		var chat = new ChatView
		{
			X      = 0,
			Y      = 0,
			Width  = Dim.Fill(),
			Height = Dim.Fill(3)
		};

		var status = new Label { X = 0, Y = Pos.AnchorEnd(2), Width = Dim.Fill(), Height = 1, Text = string.Empty };

		var info = new Label
		{
			X      = 0,
			Y      = Pos.AnchorEnd(1),
			Width  = Dim.Fill(),
			Height = 1,
			Text   = $"Esc edit prev  |  Shift+Enter newline, Enter send  |  Provider: {provider.Name}  Workdir: {workdir}"
		};
		// Tips line rendered in default color for now

		var composer = new ComposerView
		{
			X      = 0,
			Y      = Pos.AnchorEnd(3),
			Width  = Dim.Fill(),
			Height = 2,
		};

		// Layout already set by Anchors

		bool backtrackPrimed = false;
		void UpdateInfo()
		{
			var hints = backtrackPrimed ? "Esc edit prev (primed)" : "Esc edit prev";
			info.Text = $"{hints} | Shift+Enter newline; Enter send | Provider: {provider.Name} | {workdir}";
		}
		UpdateInfo();

		System.Text.StringBuilder streamBuf      = new();
		CancellationTokenSource?  currentTurnCts = null;
		const bool                streamUi       = false; // calm UX: final output only

		async Task SendAsync(string prompt)
		{
			chat.AppendUser(prompt);
			chat.BeginAssistant();
			status.Text    = "Working … (Esc to interrupt)";
			currentTurnCts = new CancellationTokenSource();
			try
			{
				var sb = new System.Text.StringBuilder();
				await runner.RunOnceAsync(sid!, prompt, onText: s => sb.Append(s), systemPrompt: system, ct: currentTurnCts.Token);
				chat.AppendAssistantChunk(sb.ToString());
			}
			catch (OperationCanceledException)
			{
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

		composer.KeyDownInner += async key =>
		{
			// Ctrl-C clears composer
			if (key.IsCtrl && key.KeyCode == Terminal.Gui.Drivers.KeyCode.C)
			{
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
			if (key.KeyCode == Terminal.Gui.Drivers.KeyCode.Esc && currentTurnCts is not null)
			{
				if (composer.IsInPasteBurst) { return; }
				currentTurnCts.Cancel();
				return;
			}

			// Esc backtrack logic (suppress during paste burst)
			if (key.KeyCode == Terminal.Gui.Drivers.KeyCode.Esc)
			{
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
						var last    = chat.GetLastUserMessageText();
						var preview = new BacktrackPreview(last);
						preview.Accepted += () =>
						{
							composer.Text   = last ?? string.Empty;
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
			if (!composer.IsInPasteBurst && key.AsRune.Value == '@' && !key.IsCtrl && !key.IsAlt)
			{
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

			// (history navigation omitted in v1 parity)

			if (key.KeyCode == Terminal.Gui.Drivers.KeyCode.Enter)
			{
				if (key.IsShift)
				{
					composer.InsertNewLine();
				}
				else
				{
					var prompt = composer.Text;
					composer.Clear();
					await SendAsync(prompt);
				}
				return;
			}

			// Ctrl+K opens file search popup and inserts selected path
			if (key.KeyCode == Terminal.Gui.Drivers.KeyCode.K && key.IsCtrl)
			{
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
        // global keybindings (reserved for future parity toggles)

        if (!string.IsNullOrWhiteSpace(initialPrompt))
        {
            Application.AddTimeout(TimeSpan.Zero, () => { _ = SendAsync(initialPrompt!); return false; });
        }

		Application.Run();
		Application.Shutdown();
		return 0;
	}
}
