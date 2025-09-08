using Hoho.Core.Repo;
using Terminal.Gui;

namespace Hoho;

internal sealed class CommitDialog : Window
{
    public CommitDialog(string workdir)
        : base("Commit All Changes", 0)
    {
        Modal = true;
        Width = Dim.Percent(70);
        Height = Dim.Percent(40);
        X = Pos.Center();
        Y = Pos.Center();

        var msgLbl = new Label("Message:") { X = 1, Y = 1 };
        var msg    = new TextField("") { X = 1, Y = 2, Width = Dim.Fill(2) };
        var ok     = new Button(" Commit ") { X = Pos.Center() - 7, Y = Pos.AnchorEnd(2) };
        var cancel = new Button(" Cancel ") { X = Pos.Center() + 2, Y = Pos.AnchorEnd(2) };

        ok.Clicked += () =>
        {
            var modal = new ApprovalModal("Confirm", "Commit all changes?");
            Application.Run(modal);
            if (modal.Result == true)
            {
                try
                {
                    var gs = new GitService(new Hoho.Core.Services.ShellRunner(), workdir);
                    gs.CommitAllAsync(msg.Text.ToString()).GetAwaiter().GetResult();
                    MessageBox.Query("Committed", "ok", "OK");
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery("Error", ex.Message, "OK");
                }
            }
            Application.RequestStop();
        };
        cancel.Clicked += () => Application.RequestStop();

        Add(msgLbl, msg, ok, cancel);
        KeyPress += e =>
        {
            if (e.KeyEvent.Key == Key.Enter) { ok.OnClicked(); e.Handled = true; }
            else if (e.KeyEvent.Key == Key.Esc) { Application.RequestStop(); e.Handled = true; }
        };
    }
}

