using Hoho.Core.Agents;
using Hoho.Core.Providers;
using Hoho.Core.Sessions;
using Terminal.Gui;

namespace Hoho;

internal static class TuiApp
{
    public static int Run(string workdir, string providerName, string? sessionId)
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

        var top = Application.Top;

        var menu = new MenuBar(new MenuBarItem[]
        {
            new("_File", new MenuItem[]
            {
                new("_New Session", "", () => { sid = store.CreateNewSessionId(); AppendLine(transcript, $"[new session: {sid}]"); }),
                new("_Quit", "", () => Application.RequestStop())
            }),
        });

        var status = new StatusBar(new StatusItem[]
        {
            new(Key.F1, $"SID: {sid}", null),
            new(Key.F2, $"Provider: {provider.Name}", null),
        });

        var transcript = new TextView
        {
            ReadOnly = true,
            WordWrap = true,
            X = 0, Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(3)
        };

        var composer = new TextView
        {
            ReadOnly = false,
            WordWrap = true,
            X = 0,
            Y = Pos.Bottom(transcript),
            Width = Dim.Fill(),
            Height = 3
        };

        composer.KeyPress += async args =>
        {
            if (args.KeyEvent.Key == Key.Enter && (args.KeyEvent.KeyModifiers & KeyModifiers.Shift) == 0)
            {
                args.Handled = true;
                var prompt = composer.Text.ToString();
                composer.Text = "";
                AppendLine(transcript, $"> {prompt}");
                try
                {
                    await runner.RunOnceAsync(sid!, prompt, onText: s =>
                    {
                        Application.MainLoop?.Invoke(() => transcript.InsertText(s));
                    });
                    AppendLine(transcript, "\n");
                }
                catch (Exception ex)
                {
                    AppendLine(transcript, $"\n[error] {ex.Message}\n");
                }
            }
        };

        top.Add(menu, transcript, composer, status);
        Application.Run();
        Application.Shutdown();
        return 0;
    }

    private static void AppendLine(TextView tv, string text)
    {
        tv.InsertText(text + (text.EndsWith("\n") ? string.Empty : "\n"));
        tv.MoveEnd();
    }
}

