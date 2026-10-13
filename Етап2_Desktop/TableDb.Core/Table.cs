namespace TableDb.Core;

public class Table
{
    public Table()
    {
    }

    public Table(string name, IEnumerable<Column> columns)
    {
        Name = name?.Trim() ?? "";
        Columns = columns.Select(c => new Column(c.Name.Trim(), c.Type,
            c.EnumValues.Select(v => v.Trim()).Where(v => v.Length > 0))).ToList();
        ValidateSchema();
    }

    public string Name { get; set; } = "";

    public List<Column> Columns { get; set; } = [];

    public List<Row> Rows { get; set; } = [];

    public int ColumnIndex(string columnName)
    {
        var index = Columns.FindIndex(c => string.Equals(c.Name, columnName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            throw new ValidationException($"У таблиці «{Name}» немає поля «{columnName}».");
        return index;
    }

    public void ValidateSchema()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new ValidationException("Назва таблиці не може бути порожньою.");
        if (Columns.Count == 0)
            throw new ValidationException($"Таблиця «{Name}» повинна мати хоча б одне поле.");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in Columns)
        {
            if (string.IsNullOrWhiteSpace(column.Name))
                throw new ValidationException("Назва поля не може бути порожньою.");
            if (!names.Add(column.Name))
                throw new ValidationException($"Поле «{column.Name}» повторюється.");
            if (column.Type == FieldType.Enum)
            {
                if (column.EnumValues.Count == 0)
                    throw new ValidationException($"Для поля «{column.Name}» типу enum потрібно задати хоча б одне значення.");
                if (column.EnumValues.Distinct(StringComparer.Ordinal).Count() != column.EnumValues.Count)
                    throw new ValidationException($"Значення enum у полі «{column.Name}» повторюються.");
            }
        }
    }

    public Row CreateValidatedRow(IReadOnlyList<string?> values)
    {
        if (values.Count != Columns.Count)
            throw new ValidationException($"Очікується {Columns.Count} значень, отримано {values.Count}.");
        return new Row(Columns.Select((c, i) => c.ValidateValue(values[i])));
    }

    public Row AddRow(IReadOnlyList<string?> values)
    {
        var row = CreateValidatedRow(values);
        Rows.Add(row);
        return row;
    }

    public void UpdateRow(int rowIndex, IReadOnlyList<string?> values)
    {
        CheckRowIndex(rowIndex);
        Rows[rowIndex] = CreateValidatedRow(values);
    }

    public void DeleteRow(int rowIndex)
    {
        CheckRowIndex(rowIndex);
        Rows.RemoveAt(rowIndex);
    }

    public IReadOnlyList<SearchResult> Search(SearchPattern pattern, string? columnName = null)
    {
        int? column = string.IsNullOrEmpty(columnName) ? null : ColumnIndex(columnName);
        var results = new List<SearchResult>();
        for (var i = 0; i < Rows.Count; i++)
        {
            var row = Rows[i];
            var match = column is { } c
                ? pattern.IsMatch(row.Values[c])
                : row.Values.Any(pattern.IsMatch);
            if (match)
                results.Add(new SearchResult(i, row));
        }
        return results;
    }

    private void CheckRowIndex(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= Rows.Count)
            throw new ValidationException($"Рядка з номером {rowIndex + 1} не існує.");
    }
}
