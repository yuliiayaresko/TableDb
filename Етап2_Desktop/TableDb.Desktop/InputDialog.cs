namespace TableDb.Desktop;

internal sealed class InputDialog : Form
{
    private readonly TextBox _textBox = new() { Dock = DockStyle.Top };

    private InputDialog(string title, string prompt, string initial)
    {
        Text = title;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = UiHelpers.DefaultFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(380, 120);
        Padding = new Padding(12);

        _textBox.Text = initial;
        var label = new Label { Text = prompt, Dock = DockStyle.Top, Height = 24 };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 36 };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 90, Height = 30 };
        var cancel = new Button { Text = "Скасувати", DialogResult = DialogResult.Cancel, Width = 90, Height = 30 };
        buttons.Controls.AddRange([cancel, ok]);
        AcceptButton = ok;
        CancelButton = cancel;

        Controls.Add(_textBox);
        Controls.Add(label);
        Controls.Add(buttons);
    }

    public static string? Show(IWin32Window owner, string title, string prompt, string initial = "")
    {
        using var dialog = new InputDialog(title, prompt, initial);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._textBox.Text : null;
    }
}
