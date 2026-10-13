using TableDb.Core;

namespace TableDb.Desktop;

public class RowEditForm : Form
{
    private readonly Table _table;
    private readonly int? _rowIndex;
    private readonly List<Control> _editors = [];
    private readonly Label _errorLabel = new() { Dock = DockStyle.Bottom, Height = 48, ForeColor = Color.Firebrick };

    public RowEditForm(Table table, int? rowIndex)
    {
        _table = table;
        _rowIndex = rowIndex;
        Text = rowIndex is null ? $"Новий рядок — {table.Name}" : $"Редагування рядка {rowIndex + 1} — {table.Name}";
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = UiHelpers.DefaultFont;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        Padding = new Padding(14);
        AutoScroll = true;

        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        var current = rowIndex is { } i ? table.Rows[i].Values : null;

        for (var c = 0; c < table.Columns.Count; c++)
        {
            var column = table.Columns[c];
            var value = current?[c] ?? "";
            Control editor;
            if (column.Type == FieldType.Enum)
            {
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 290 };
                combo.Items.Add("");
                combo.Items.AddRange(column.EnumValues.ToArray<object>());
                combo.SelectedItem = column.EnumValues.Contains(value) ? value : "";
                editor = combo;
            }
            else
            {
                editor = new TextBox
                {
                    Width = 290,
                    Text = value,
                    PlaceholderText = column.DataType.FormatHint,
                    MaxLength = column.Type == FieldType.Char ? 1 : 32767
                };
            }
            _editors.Add(editor);
            layout.Controls.Add(new Label { Text = UiHelpers.HeaderText(column), AutoSize = true, Margin = new Padding(0, 7, 8, 7) }, 0, c);
            layout.Controls.Add(editor, 1, c);
        }

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40, Padding = new Padding(0, 6, 0, 0) };
        var ok = new Button { Text = rowIndex is null ? "Додати" : "Зберегти", Width = 110, Height = 32 };
        ok.Click += (_, _) => TryAccept();
        var cancel = new Button { Text = "Скасувати", DialogResult = DialogResult.Cancel, Width = 110, Height = 32 };
        buttons.Controls.AddRange([cancel, ok]);
        AcceptButton = ok;
        CancelButton = cancel;

        Controls.Add(layout);
        Controls.Add(_errorLabel);
        Controls.Add(buttons);
        ClientSize = new Size(530, Math.Min(620, table.Columns.Count * 38 + 130));
    }

    public IReadOnlyList<string?> ReadValues() =>
        _editors.Select(e => e is ComboBox cb ? cb.SelectedItem as string : e.Text).ToList();

    public void SetValues(IReadOnlyList<string> values)
    {
        for (var i = 0; i < _editors.Count && i < values.Count; i++)
        {
            if (_editors[i] is ComboBox cb)
                cb.SelectedItem = values[i];
            else
                _editors[i].Text = values[i];
        }
    }

    public bool TryAccept()
    {
        try
        {
            var values = ReadValues();
            if (_rowIndex is { } index)
                _table.UpdateRow(index, values);
            else
                _table.AddRow(values);
            DialogResult = DialogResult.OK;
            return true;
        }
        catch (ValidationException ex)
        {
            _errorLabel.Text = "⚠ " + ex.Message;
            return false;
        }
    }
}
