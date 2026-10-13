using TableDb.Core;

namespace TableDb.Desktop;

internal static class UiHelpers
{
    public static readonly Font DefaultFont = new("Segoe UI", 9.75f);

    public static string TypeLabel(Column column) => column.Type == FieldType.Enum
        ? $"enum{{{string.Join(", ", column.EnumValues)}}}"
        : column.DataType.DisplayName;

    public static string HeaderText(Column column) => $"{column.Name} ({column.DataType.DisplayName})";

    public static void ShowError(IWin32Window owner, string message) =>
        MessageBox.Show(owner, message, "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static DataGridView CreateGrid(bool readOnly)
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = readOnly,
            AllowUserToAddRows = !readOnly,
            AllowUserToDeleteRows = !readOnly,
            AllowUserToResizeRows = false,
            SelectionMode = readOnly ? DataGridViewSelectionMode.FullRowSelect : DataGridViewSelectionMode.CellSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.FixedSingle,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        };
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(232, 238, 247);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font(DefaultFont, FontStyle.Bold);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 253);
        return grid;
    }

    public static void FillGrid(DataGridView grid, Table table, IEnumerable<(int Index, Row Row)> rows, string numberHeader = "№")
    {
        grid.SuspendLayout();
        grid.Columns.Clear();
        var number = new DataGridViewTextBoxColumn { HeaderText = numberHeader, FillWeight = 12, MinimumWidth = 45 };
        number.DefaultCellStyle.ForeColor = Color.DimGray;
        grid.Columns.Add(number);
        foreach (var column in table.Columns)
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = HeaderText(column), SortMode = DataGridViewColumnSortMode.NotSortable });
        foreach (var (index, row) in rows)
        {
            var cells = new object[table.Columns.Count + 1];
            cells[0] = index + 1;
            for (var i = 0; i < table.Columns.Count; i++)
                cells[i + 1] = (i < row.Values.Count ? row.Values[i] : null) ?? "";
            var rowIndex = grid.Rows.Add(cells);
            grid.Rows[rowIndex].Tag = index;
        }

        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        grid.Columns[^1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        grid.ResumeLayout();
    }

    public static Button CreateButton(string text, EventHandler onClick, int width = 130)
    {
        var button = new Button { Text = text, AutoSize = true, MinimumSize = new Size(width, 32), Padding = new Padding(8, 0, 8, 0), Margin = new Padding(0, 0, 8, 0) };
        button.Click += onClick;
        return button;
    }
}
