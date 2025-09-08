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

        // Single column chat list + composer + info line
        var chat = new ChatView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(2)
        };

        var info = new Label
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
            Text = $"Mode: workspace-write  Approval: on-failure  Provider: {provider.Name}  Workdir: {workdir}"
        };

        var composer = new TextField("")
        {
            X = 0,
            Y = Pos.AnchorEnd(2),
            Width = Dim.Fill(),
            Height = 1,
        };

        async Task SendAsync(string prompt)
        {
            chat.AppendUser(prompt);
            chat.BeginAssistant();
            try
            {
                await runner.RunOnceAsync(sid!, prompt, onText: s =>
                {
                    Application.MainLoop?.Invoke(() => chat.AppendAssistantChunk(s));
                }, systemPrompt: system);
            }
            catch (Exception ex)
            {
                chat.AppendInfo($"error: {ex.Message}");
            }
            finally
            {
                chat.EndAssistant();
            }
        }

        composer.KeyPress += async args =>
        {
            if (args.KeyEvent.Key == Key.Enter)
            {
                args.Handled = true;
                var prompt = composer.Text.ToString();
                composer.Text = "";
                await SendAsync(prompt);
            }
        };

        top.Add(chat, composer, info);

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
