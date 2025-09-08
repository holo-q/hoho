using Hoho.Core.Sandbox;
using Hoho.Core.Tools;
using Terminal.Gui;

namespace Hoho;

internal sealed class ApplyPatchDialog : Window
{
    private readonly TextView _input;
    private readonly Label _hint;
    private readonly string _workdir;
    private readonly Hoho.Core.Sandbox.SandboxMode _sandbox;
    private readonly Hoho.Core.Sandbox.ApprovalPolicy _approval;

    public ApplyPatchDialog(string workdir, Hoho.Core.Sandbox.SandboxMode sandbox, Hoho.Core.Sandbox.ApprovalPolicy approval)
        : base("Apply Patch", 0)
    {
        _workdir = workdir; _sandbox = sandbox; _approval = approval;
        Modal = true;
        Width = Dim.Percent(80);
        Height = Dim.Percent(70);
        X = Pos.Center();
        Y = Pos.Center();

        _input = new TextView { X = 1, Y = 1, Width = Dim.Fill(2), Height = Dim.Fill(3), WordWrap = false };
        _hint = new Label("Paste apply_patch envelope. Enter to confirm, Esc to cancel.") { X = 1, Y = Pos.AnchorEnd(2) };
        Add(_input, _hint);

        KeyPress += e =>
        {
            if (e.KeyEvent.Key == Key.Enter)
            {
                e.Handled = true;
                var approved = _approval switch
                {
                    Hoho.Core.Sandbox.ApprovalPolicy.Never => true,
                    Hoho.Core.Sandbox.ApprovalPolicy.OnFailure => true,
                    Hoho.Core.Sandbox.ApprovalPolicy.OnRequest => Ask(),
                    Hoho.Core.Sandbox.ApprovalPolicy.Untrusted => Ask(),
                    _ => Ask(),
                };
                if (approved)
                {
                    try
                    {
                        var fs = new FileService(_workdir, _sandbox);
                        var ps = new PatchService(fs);
                        var result = ps.ApplyAsync(_input.Text.ToString()).GetAwaiter().GetResult();
                        MessageBox.Query("Patch Applied", result.ToString(), "OK");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.ErrorQuery("Error", ex.Message, "OK");
                    }
                }
                Application.RequestStop();
            }
            else if (e.KeyEvent.Key == Key.Esc)
            {
                e.Handled = true;
                Application.RequestStop();
            }
        };
    }

    private static bool Ask()
    {
        var modal = new ApprovalModal("Confirm", "Apply patch to workspace?");
        Application.Run(modal);
        return modal.Result == true;
    }
}
