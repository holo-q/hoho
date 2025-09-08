using Hoho.Core.Sandbox;
using Hoho.Core.Tools;
using Terminal.Gui;

namespace Hoho;

internal sealed class ApplyPatchDialog : Window
{
    private readonly TextView _input;
    private readonly Label _hint;
    public ApplyPatchDialog() : base("Apply Patch", 0)
    {
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
                var modal = new ApprovalModal("Confirm", "Apply patch to workspace?");
                Application.Run(modal);
                if (modal.Result == true)
                {
                    try
                    {
                        var fs = new FileService(Environment.CurrentDirectory, SandboxMode.WorkspaceWrite);
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
}

