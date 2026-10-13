namespace TableDb.Desktop;

internal sealed class ChooseDialog : Form
{
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle };

    private ChooseDialog(string title, string prompt, IEnumerable<string> items)
    {
        Text = title;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = UiHelpers.DefaultFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(400, 320);
        Padding = new Padding(12);

        foreach (var item in items)
            _list.Items.Add(item);
        if (_list.Items.Count > 0)
            _list.SelectedIndex = 0;

        var label = new Label { Text = prompt, Dock = DockStyle.Top, Height = 24 };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40 };
        var ok = new Button { Text = "Відкрити", DialogResult = DialogResult.OK, Width = 100, Height = 30 };
        var cancel = new Button { Text = "Скасувати", DialogResult = DialogResult.Cancel, Width = 100, Height = 30 };
        buttons.Controls.AddRange([cancel, ok]);
        AcceptButton = ok;
        CancelButton = cancel;

        _list.DoubleClick += (_, _) =>
        {
            if (_list.SelectedItem is not null)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        };

        Controls.Add(_list);
        Controls.Add(buttons);
        Controls.Add(label);
    }

    public static string? Show(IWin32Window owner, string title, string prompt, IEnumerable<string> items)
    {
        using var dialog = new ChooseDialog(title, prompt, items);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._list.SelectedItem as string : null;
    }
}
