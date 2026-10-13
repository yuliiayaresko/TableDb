using TableDb.Core;

namespace TableDb.Desktop;

public class CreateTableForm : Form
{
    private static readonly string[] TypeNames = Enum.GetValues<FieldType>().Select(t => DataTypeFactory.Create(t).DisplayName).ToArray();

    private readonly Database _database;
    private readonly TextBox _nameBox = new() { Width = 300 };
    private readonly DataGridView _columnsGrid = UiHelpers.CreateGrid(readOnly: false);
    private readonly Label _errorLabel = new() { Dock = DockStyle.Bottom, Height = 44, ForeColor = Color.Firebrick, AutoEllipsis = true };

    public CreateTableForm(Database database)
    {
        _database = database;
        Text = "Створення таблиці";
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = UiHelpers.DefaultFont;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Size = new Size(720, 480);
        MinimumSize = new Size(600, 380);
        Padding = new Padding(12);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, WrapContents = false };
        top.Controls.Add(new Label { Text = "Назва таблиці:", AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
        top.Controls.Add(_nameBox);

        var hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 56,
            ForeColor = Color.DimGray,
            Text = "Поля таблиці. Типи: integer, real, char, string, enum, email.\n" +
                   "Для enum вкажіть допустимі значення через «;», напр.: денна; заочна"
        };

        _columnsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Назва поля", FillWeight = 35 });
        var typeColumn = new DataGridViewComboBoxColumn { HeaderText = "Тип", FillWeight = 20, FlatStyle = FlatStyle.Flat };
        typeColumn.Items.AddRange(TypeNames);
        _columnsGrid.Columns.Add(typeColumn);
        _columnsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Значення enum (через ;)", FillWeight = 45 });
        _columnsGrid.DefaultValuesNeeded += (_, e) => e.Row.Cells[1].Value = "string";
        _columnsGrid.Rows.Add("Id", "integer", "");

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40, Padding = new Padding(0, 6, 0, 0) };
        var ok = new Button { Text = "Створити", Width = 110, Height = 32 };
        ok.Click += (_, _) => TryCreate();
        var cancel = new Button { Text = "Скасувати", DialogResult = DialogResult.Cancel, Width = 110, Height = 32 };
        buttons.Controls.AddRange([cancel, ok]);
        CancelButton = cancel;

        var gridHost = new Panel { Dock = DockStyle.Fill };
        gridHost.Controls.Add(_columnsGrid);
        Controls.Add(gridHost);
        Controls.Add(hint);
        Controls.Add(top);
        Controls.Add(_errorLabel);
        Controls.Add(buttons);
    }

    public Table? CreatedTable { get; private set; }

    public List<Column> ReadColumns()
    {
        _columnsGrid.EndEdit();
        var columns = new List<Column>();
        foreach (DataGridViewRow row in _columnsGrid.Rows)
        {
            if (row.IsNewRow)
                continue;
            var name = (row.Cells[0].Value as string ?? "").Trim();
            var typeName = row.Cells[1].Value as string ?? "string";
            var enumText = row.Cells[2].Value as string ?? "";
            if (name.Length == 0 && enumText.Trim().Length == 0)
                continue;
            var type = Enum.GetValues<FieldType>()[Array.IndexOf(TypeNames, typeName)];
            var values = type == FieldType.Enum ? enumText.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) : [];
            columns.Add(new Column(name, type, values));
        }
        return columns;
    }

    private void TryCreate()
    {
        try
        {
            CreatedTable = _database.CreateTable(_nameBox.Text, ReadColumns());
            DialogResult = DialogResult.OK;
        }
        catch (ValidationException ex)
        {
            ShowError(ex.Message);
        }
    }

    public void ShowError(string message) => _errorLabel.Text = "⚠ " + message;

    public void Fill(string tableName, IEnumerable<(string Name, string Type, string EnumValues)> columns)
    {
        _nameBox.Text = tableName;
        _columnsGrid.Rows.Clear();
        foreach (var (name, type, values) in columns)
            _columnsGrid.Rows.Add(name, type, values);
    }
}
