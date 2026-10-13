using TableDb.Core;

namespace TableDb.Desktop;

public class MainForm : Form
{
    private const string AllColumns = "(усі поля)";
    private const string FileFilter = "Таблична база (*.tdb.json)|*.tdb.json|Усі файли (*.*)|*.*";

    private readonly IDatabaseStorage _storage = new JsonDatabaseStorage();
    private Database? _database;
    private string? _filePath;
    private bool _isDirty;

    private readonly ListBox _tablesList = new() { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle };
    private readonly Label _dbLabel = new() { Dock = DockStyle.Top, Height = 28, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label _schemaLabel = new() { Dock = DockStyle.Top, Height = 26, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray, AutoEllipsis = true };
    private readonly Label _tableTitle = new() { Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleLeft };
    private readonly DataGridView _grid = UiHelpers.CreateGrid(readOnly: true);
    private readonly TextBox _patternBox = new() { Width = 210, PlaceholderText = "напр. *@gmail.com або Іван*" };
    private readonly ComboBox _columnBox = new() { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _caseBox = new() { Text = "з урахуванням регістру", AutoSize = true, Margin = new Padding(0, 4, 16, 0) };
    private readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly List<Control> _tableDependent = [];
    private readonly List<ToolStripItem> _tableDependentMenu = [];
    private readonly List<ToolStripItem> _dbDependentMenu = [];
    private Button _createTableButton = null!;

    public MainForm()
    {
        Text = "TableDb";
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = UiHelpers.DefaultFont;
        MinimumSize = new Size(900, 560);
        Size = new Size(1260, 720);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        BuildLayout();
        MainMenuStrip = BuildMenu();
        Controls.Add(MainMenuStrip);

        _tablesList.SelectedIndexChanged += (_, _) => ShowSelectedTable();
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditRow(); };
        _grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete)
            {
                e.Handled = true;
                DeleteRow();
            }
        };
        _patternBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                RunSearch();
            }
        };
        FormClosing += (_, e) => e.Cancel = !ConfirmDiscardChanges();

        UpdateUi();
    }

    public Database? Database => _database;

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip { Font = UiHelpers.DefaultFont };

        var file = new ToolStripMenuItem("&Файл");
        file.DropDownItems.Add(new ToolStripMenuItem("Нова база…", null, (_, _) => NewDatabase(), Keys.Control | Keys.N));
        file.DropDownItems.Add(new ToolStripMenuItem("Відкрити…", null, (_, _) => OpenDatabase(), Keys.Control | Keys.O));
        var save = new ToolStripMenuItem("Зберегти", null, (_, _) => SaveDatabase(false), Keys.Control | Keys.S);
        var saveAs = new ToolStripMenuItem("Зберегти як…", null, (_, _) => SaveDatabase(true), Keys.Control | Keys.Shift | Keys.S);
        file.DropDownItems.AddRange([save, saveAs, new ToolStripSeparator(), new ToolStripMenuItem("Вихід", null, (_, _) => Close())]);
        _dbDependentMenu.AddRange([save, saveAs]);

        var table = new ToolStripMenuItem("&Таблиця");
        var create = new ToolStripMenuItem("Створити таблицю…", null, (_, _) => CreateTable(), Keys.Control | Keys.T);
        var drop = new ToolStripMenuItem("Видалити таблицю", null, (_, _) => DropTable());
        table.DropDownItems.AddRange([create, drop]);
        _dbDependentMenu.Add(create);
        _tableDependentMenu.Add(drop);

        var rows = new ToolStripMenuItem("&Рядки");
        var add = new ToolStripMenuItem("Додати рядок…", null, (_, _) => AddRow(), Keys.Control | Keys.R);
        var edit = new ToolStripMenuItem("Редагувати рядок…", null, (_, _) => EditRow(), Keys.F2);

        var delete = new ToolStripMenuItem("Видалити рядок", null, (_, _) => DeleteRow()) { ShortcutKeyDisplayString = "Del" };
        var search = new ToolStripMenuItem("Пошук за шаблоном", null, (_, _) => _patternBox.Focus(), Keys.Control | Keys.F);
        rows.DropDownItems.AddRange([add, edit, delete, new ToolStripSeparator(), search]);
        _tableDependentMenu.AddRange([add, edit, delete, search]);

        var help = new ToolStripMenuItem("&Довідка");
        help.DropDownItems.Add(new ToolStripMenuItem("Про програму", null, (_, _) => MessageBox.Show(this,
            "TableDb — часткова реалізація СУ табличними базами даних.\n\n" +
            "Типи полів: integer, real, char, string, enum, email.\n" +
            "Операція варіанту: пошук за шаблоном (* — будь-які символи, ? — один символ).\n\n" +
            "Автор: Яресько Юлія Олександрівна, ТТП-41, варіант 41.",
            "Про програму", MessageBoxButtons.OK, MessageBoxIcon.Information)));

        menu.Items.AddRange([file, table, rows, help]);
        return menu;
    }

    private void BuildLayout()
    {
        var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };

        Load += (_, _) => split.SplitterDistance = LogicalToDeviceUnits(240);

        var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 8, 6, 10) };
        _dbLabel.Font = new Font(UiHelpers.DefaultFont, FontStyle.Bold);
        var tablesCaption = new Label { Text = "Таблиці:", Dock = DockStyle.Top, Height = 22 };
        var leftButtons = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 40, ColumnCount = 2, Padding = new Padding(0, 6, 0, 0) };
        leftButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        leftButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _createTableButton = new Button { Text = "Створити", Dock = DockStyle.Fill };
        _createTableButton.Click += (_, _) => CreateTable();
        var dropButton = new Button { Text = "Видалити", Dock = DockStyle.Fill };
        dropButton.Click += (_, _) => DropTable();
        leftButtons.Controls.Add(_createTableButton, 0, 0);
        leftButtons.Controls.Add(dropButton, 1, 0);
        _tableDependent.Add(dropButton);
        left.Controls.Add(_tablesList);
        left.Controls.Add(tablesCaption);
        left.Controls.Add(_dbLabel);
        left.Controls.Add(leftButtons);
        split.Panel1.Controls.Add(left);

        var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 8, 10, 10) };
        var searchPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 72, WrapContents = true, Padding = new Padding(8, 6, 8, 0), BackColor = Color.FromArgb(240, 244, 250) };
        var searchButton = UiHelpers.CreateButton("Знайти", (_, _) => RunSearch(), 90);
        searchButton.MinimumSize = new Size(90, 27);
        searchButton.Margin = new Padding(10, 0, 0, 0);
        searchPanel.Controls.Add(new Label { Text = "Пошук за шаблоном:", AutoSize = true, Margin = new Padding(0, 5, 4, 0) });
        searchPanel.Controls.Add(_patternBox);
        searchPanel.Controls.Add(new Label { Text = "у полі:", AutoSize = true, Margin = new Padding(8, 5, 4, 0) });
        searchPanel.Controls.Add(_columnBox);
        searchPanel.Controls.Add(searchButton);
        searchPanel.Controls.Add(_caseBox);
        searchPanel.SetFlowBreak(searchButton, true);
        var hint = new Label { Text = "шаблон:  * — будь-які символи,  ? — один символ", AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 6, 0, 0) };
        searchPanel.Controls.Add(hint);
        _tableDependent.AddRange([_patternBox, _columnBox, _caseBox, searchButton]);

        _tableTitle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(0, 8, 0, 0) };
        var addButton = UiHelpers.CreateButton("Додати рядок", (_, _) => AddRow());
        var editButton = UiHelpers.CreateButton("Редагувати", (_, _) => EditRow());
        var deleteButton = UiHelpers.CreateButton("Видалити рядок", (_, _) => DeleteRow());
        bottom.Controls.AddRange([addButton, editButton, deleteButton]);
        _tableDependent.AddRange([addButton, editButton, deleteButton]);

        var gridHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 0) };
        gridHost.Controls.Add(_grid);

        right.Controls.Add(gridHost);
        right.Controls.Add(_schemaLabel);
        right.Controls.Add(_tableTitle);
        right.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 6 });
        right.Controls.Add(searchPanel);
        right.Controls.Add(bottom);
        split.Panel2.Controls.Add(right);

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_status);

        Controls.Add(split);
        Controls.Add(statusStrip);
    }

    private Table? CurrentTable =>
        _database is not null && _tablesList.SelectedItem is string name ? _database.FindTable(name) : null;

    private void NewDatabase()
    {
        if (!ConfirmDiscardChanges())
            return;
        var name = InputDialog.Show(this, "Нова база даних", "Назва бази:", "Моя база");
        if (name is null)
            return;
        try
        {
            SetDatabase(new Database(name), null);
            MarkDirty();
            SetStatus($"Створено нову базу «{_database!.Name}».");
        }
        catch (ValidationException ex)
        {
            UiHelpers.ShowError(this, ex.Message);
        }
    }

    private void OpenDatabase()
    {
        if (!ConfirmDiscardChanges())
            return;
        using var dialog = new OpenFileDialog { Filter = FileFilter, Title = "Відкрити базу" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            OpenFile(dialog.FileName);
    }

    public void OpenFile(string path)
    {
        try
        {
            SetDatabase(_storage.Load(path), path);
            SetStatus($"Базу завантажено з {path}");
        }
        catch (Exception ex) when (ex is ValidationException or IOException or UnauthorizedAccessException)
        {
            UiHelpers.ShowError(this, "Не вдалося відкрити базу:\n" + ex.Message);
        }
    }

    private bool SaveDatabase(bool saveAs)
    {
        if (_database is null)
            return false;
        var path = _filePath;
        if (saveAs || path is null)
        {
            using var dialog = new SaveFileDialog
            {
                Filter = FileFilter,
                Title = "Зберегти базу",
                FileName = _database.Name + JsonDatabaseStorage.FileExtension
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return false;
            path = dialog.FileName;
        }
        try
        {
            _storage.Save(_database, path);
            _filePath = path;
            _isDirty = false;
            UpdateUi();
            SetStatus($"Базу збережено у {path}");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UiHelpers.ShowError(this, "Не вдалося зберегти базу:\n" + ex.Message);
            return false;
        }
    }

    public void SetDatabase(Database database, string? path)
    {
        _database = database;
        _filePath = path;
        _isDirty = false;
        RefreshTables(database.Tables.FirstOrDefault()?.Name);
    }

    private bool ConfirmDiscardChanges()
    {
        if (!_isDirty || _database is null)
            return true;
        var answer = MessageBox.Show(this, $"Зберегти зміни в базі «{_database.Name}»?", "Незбережені зміни",
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        return answer switch
        {
            DialogResult.Yes => SaveDatabase(false),
            DialogResult.No => true,
            _ => false
        };
    }

    private void CreateTable()
    {
        if (_database is null)
            return;
        using var dialog = new CreateTableForm(_database);
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.CreatedTable is { } table)
        {
            MarkDirty();
            RefreshTables(table.Name);
            SetStatus($"Створено таблицю «{table.Name}» з {table.Columns.Count} полями.");
        }
    }

    private void DropTable()
    {
        if (_database is null || CurrentTable is not { } table)
            return;
        var answer = MessageBox.Show(this, $"Видалити таблицю «{table.Name}» ({table.Rows.Count} рядків)?",
            "Видалення таблиці", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes)
            return;
        _database.DropTable(table.Name);
        MarkDirty();
        RefreshTables(null);
        SetStatus($"Таблицю «{table.Name}» видалено.");
    }

    private void RefreshTables(string? selectName)
    {
        _tablesList.BeginUpdate();
        _tablesList.Items.Clear();
        if (_database is not null)
            foreach (var name in _database.TableNames)
                _tablesList.Items.Add(name);
        _tablesList.EndUpdate();

        if (selectName is not null && _tablesList.Items.Contains(selectName))
            _tablesList.SelectedItem = selectName;
        else if (_tablesList.Items.Count > 0)
            _tablesList.SelectedIndex = 0;
        else
            ShowSelectedTable();
        UpdateUi();
    }

    public void SelectTable(string name) => _tablesList.SelectedItem = name;

    private void ShowSelectedTable()
    {
        var table = CurrentTable;
        _columnBox.Items.Clear();
        _columnBox.Items.Add(AllColumns);
        if (table is null)
        {
            _grid.Columns.Clear();
            _tableTitle.Text = _database is null ? "Створіть або відкрийте базу даних" : "Створіть таблицю";
            _schemaLabel.Text = "";
        }
        else
        {
            foreach (var column in table.Columns)
                _columnBox.Items.Add(column.Name);
            _tableTitle.Text = $"{table.Name}  —  {table.Rows.Count} рядк(ів)";
            _schemaLabel.Text = "Схема: " + string.Join(";  ", table.Columns.Select(c => $"{c.Name}: {UiHelpers.TypeLabel(c)}"));
            UiHelpers.FillGrid(_grid, table, table.Rows.Select((r, i) => (i, r)));
        }
        _columnBox.SelectedIndex = 0;
        UpdateUi();
    }

    private int? SelectedRowIndex => _grid.CurrentRow?.Tag as int?;

    private void AddRow()
    {
        if (CurrentTable is not { } table)
            return;
        using var dialog = new RowEditForm(table, null);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        MarkDirty();
        ShowSelectedTable();
        SelectGridRow(table.Rows.Count - 1);
        SetStatus("Рядок додано.");
    }

    private void EditRow()
    {
        if (CurrentTable is not { } table || SelectedRowIndex is not { } index)
            return;
        using var dialog = new RowEditForm(table, index);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        MarkDirty();
        ShowSelectedTable();
        SelectGridRow(index);
        SetStatus($"Рядок {index + 1} змінено.");
    }

    private void DeleteRow()
    {
        if (CurrentTable is not { } table || SelectedRowIndex is not { } index)
            return;
        if (MessageBox.Show(this, $"Видалити рядок {index + 1}?", "Видалення рядка", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        table.DeleteRow(index);
        MarkDirty();
        ShowSelectedTable();
        SelectGridRow(Math.Min(index, table.Rows.Count - 1));
        SetStatus($"Рядок {index + 1} видалено.");
    }

    private void SelectGridRow(int index)
    {
        if (index < 0 || index >= _grid.Rows.Count)
            return;
        _grid.ClearSelection();
        _grid.CurrentCell = _grid.Rows[index].Cells[0];
        _grid.Rows[index].Selected = true;
    }

    private void RunSearch()
    {
        if (CurrentTable is not { } table)
            return;
        var column = _columnBox.SelectedItem as string;
        if (column == AllColumns)
            column = null;
        try
        {
            var results = SearchService.Search(table, _patternBox.Text, column, _caseBox.Checked);
            SetStatus($"Знайдено {results.Count} з {table.Rows.Count} рядків за шаблоном «{_patternBox.Text}».");
            using var dialog = new SearchResultsForm(table, _patternBox.Text, column, _caseBox.Checked, results);
            if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedRowIndex is { } index)
                SelectGridRow(index);
        }
        catch (ValidationException ex)
        {
            UiHelpers.ShowError(this, ex.Message);
            _patternBox.Focus();
        }
    }

    private void MarkDirty()
    {
        _isDirty = true;
        UpdateUi();
    }

    private void UpdateUi()
    {
        var hasDb = _database is not null;
        var hasTable = CurrentTable is not null;
        foreach (var control in _tableDependent)
            control.Enabled = hasTable;
        foreach (var item in _tableDependentMenu)
            item.Enabled = hasTable;
        foreach (var item in _dbDependentMenu)
            item.Enabled = hasDb;
        _createTableButton.Enabled = hasDb;

        _dbLabel.Text = hasDb ? "База: " + _database!.Name : "База не відкрита";
        var fileName = _filePath is null ? "не збережено" : Path.GetFileName(_filePath);
        Text = hasDb ? $"TableDb — {_database!.Name}{(_isDirty ? " *" : "")} ({fileName})" : "TableDb";
        if (!hasDb)
            SetStatus("Створіть нову базу (Ctrl+N) або відкрийте існуючу (Ctrl+O).");
    }

    private void SetStatus(string text) => _status.Text = text;
}
