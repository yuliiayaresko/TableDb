namespace TableDb.Core;

public class Database
{
    public Database()
    {
    }

    public Database(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Назва бази не може бути порожньою.");
        Name = name.Trim();
    }

    public string Name { get; set; } = "";

    public List<Table> Tables { get; set; } = [];

    public IEnumerable<string> TableNames => Tables.Select(t => t.Name);

    public Table CreateTable(string name, IEnumerable<Column> columns)
    {
        var table = new Table(name, columns);
        if (FindTable(table.Name) is not null)
            throw new ValidationException($"Таблиця «{table.Name}» вже існує.");
        Tables.Add(table);
        return table;
    }

    public void DropTable(string name)
    {
        var table = GetTable(name);
        Tables.Remove(table);
    }

    public Table? FindTable(string name) =>
        Tables.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    public Table GetTable(string name) =>
        FindTable(name) ?? throw new ValidationException($"Таблиці «{name}» не існує.");
}
