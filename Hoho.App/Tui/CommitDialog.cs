using Hoho.Core.Repo;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Hoho;

internal sealed class CommitDialog : Window
{
    private readonly Core.Sandbox.ApprovalPolicy _approval;
    public CommitDialog(string workdir, Core.Sandbox.ApprovalPolicy approval)
    {
        Title = "Commit All Changes";
        _approval = approval;
        Modal = true;
        Width = Dim.Percent(70);
        Height = Dim.Percent(40);
        X = Pos.Center();
        Y = Pos.Center();

        var msgLbl = new Label { Text = "Message:", X = 1, Y = 1 };
        var msg    = new TextField { Text = "", X = 1, Y = 2, Width = Dim.Fill(2) };
        var ok     = new Button { Text = " Commit ", X = Pos.Center() - 7, Y = Pos.AnchorEnd(2) };
        var cancel = new Button { Text = " Cancel ", X = Pos.Center() + 2, Y = Pos.AnchorEnd(2) };

        ok.Accepting += (s,e) =>
        {
            var approved = _approval switch
            {
                Core.Sandbox.ApprovalPolicy.Never => true,
                Core.Sandbox.ApprovalPolicy.OnFailure => true,
                Core.Sandbox.ApprovalPolicy.OnRequest => AskCommit(),
                Core.Sandbox.ApprovalPolicy.Untrusted => AskCommit(),
                _ => AskCommit(),
            };
            if (approved)
            {
                try
                {
                    var gs = new GitService(new Core.Services.ShellRunner(), workdir);
                    gs.CommitAllAsync(msg.Text.ToString()).GetAwaiter().GetResult();
                    MessageBox.Query("Committed", "ok", "OK");
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery("Error", ex.Message, "OK");
                }
            }
            Application.RequestStop();
            e.Handled = true;
        };
        cancel.Accepting += (s,e) => { Application.RequestStop(); e.Handled = true; };

        Add(msgLbl, msg, ok, cancel);
        KeyDown += (s, key) =>
        {
            if (key == Key.Enter) { ok.InvokeCommand(Command.Accept); }
            else if (key == Key.Esc) { Application.RequestStop(); }
        };
    }

    private static bool AskCommit()
    {
        var modal = new ApprovalModal("Confirm", "Commit all changes?");
        Application.Run(modal);
        return modal.Result == true;
    }
}
