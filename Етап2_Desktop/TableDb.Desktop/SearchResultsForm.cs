using TableDb.Core;

namespace TableDb.Desktop;

public class SearchResultsForm : Form
{
    private readonly DataGridView _grid = UiHelpers.CreateGrid(readOnly: true);

    public SearchResultsForm(Table table, string pattern, string? column, bool caseSensitive, IReadOnlyList<SearchResult> results)
    {
        Text = "Результати пошуку";
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = UiHelpers.DefaultFont;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Size = new Size(1000, 420);
        Padding = new Padding(12);

        var where = column is null ? "в усіх полях" : $"у полі «{column}»";
        var summary = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            Font = new Font(UiHelpers.DefaultFont, FontStyle.Bold),
            Text = $"Таблиця «{table.Name}»: шаблон «{pattern}» {where}{(caseSensitive ? " (з урахуванням регістру)" : "")} — знайдено {results.Count} з {table.Rows.Count} рядків"
        };
        var hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            ForeColor = Color.DimGray,
            Text = results.Count == 0 ? "Жоден рядок не відповідає шаблону." : "Подвійний клік по рядку — перейти до нього в таблиці."
        };

        UiHelpers.FillGrid(_grid, table, results.Select(r => (r.RowIndex, r.Row)), "№ рядка");
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) GoToRow(); };

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 42, Padding = new Padding(0, 8, 0, 0) };
        var close = new Button { Text = "Закрити", DialogResult = DialogResult.Cancel, Width = 110, Height = 32 };
        var go = new Button { Text = "Перейти до рядка", AutoSize = true, MinimumSize = new Size(150, 32), Padding = new Padding(8, 0, 8, 0), Enabled = results.Count > 0 };
        go.Click += (_, _) => GoToRow();
        buttons.Controls.AddRange([close, go]);
        CancelButton = close;

        var gridHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 0) };
        gridHost.Controls.Add(_grid);
        Controls.Add(gridHost);
        Controls.Add(hint);
        Controls.Add(summary);
        Controls.Add(buttons);
    }

    public int? SelectedRowIndex { get; private set; }

    private void GoToRow()
    {
        if (_grid.CurrentRow?.Tag is int index)
        {
            SelectedRowIndex = index;
            DialogResult = DialogResult.OK;
        }
    }
}
