using Hoho.Core.Sandbox;
using Hoho.Core.Tools;
using Terminal.Gui;
using Terminal.Gui.Input;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.App;

namespace Hoho;

internal sealed class ApplyPatchDialog : Window
{
    private readonly TextView _input;
    private readonly Label _hint;
    private readonly string _workdir;
    private readonly SandboxMode _sandbox;
    private readonly ApprovalPolicy _approval;

    public ApplyPatchDialog(string workdir, SandboxMode sandbox, ApprovalPolicy approval)
    {
        Title = "Apply Patch";
        _workdir = workdir; _sandbox = sandbox; _approval = approval;
        Modal = true;
        Width = Dim.Percent(80);
        Height = Dim.Percent(70);
        X = Pos.Center();
        Y = Pos.Center();

        _input = new TextView { X = 1, Y = 1, Width = Dim.Fill(2), Height = Dim.Fill(3), WordWrap = false };
        _hint = new Label { Text = "Paste apply_patch envelope. Enter to confirm, Esc to cancel.", X = 1, Y = Pos.AnchorEnd(2) };
        Add(_input, _hint);

        KeyDown += (s, key) =>
        {
            if (key == Key.Enter)
            {
                var approved = _approval switch
                {
                    ApprovalPolicy.Never => true,
                    ApprovalPolicy.OnFailure => true,
                    ApprovalPolicy.OnRequest => Ask(),
                    ApprovalPolicy.Untrusted => Ask(),
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
            else if (key == Key.Esc)
            {
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
