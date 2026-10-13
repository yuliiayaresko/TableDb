using TableDb.Core;

namespace TableDb.Web;

public class NotFoundException(string message) : Exception(message);

public class DatabaseService
{
    private readonly object _sync = new();
    private readonly Dictionary<string, OpenDatabase> _open = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDatabaseStorage _storage = new JsonDatabaseStorage();
    private readonly string _dataDir;

    private sealed class OpenDatabase(Database database)
    {
        public Database Database { get; set; } = database;
        public bool IsDirty { get; set; }
    }

    public DatabaseService(string dataDir)
    {
        _dataDir = dataDir;
        Directory.CreateDirectory(_dataDir);
    }

    public IReadOnlyList<DatabaseInfo> List()
    {
        lock (_sync)
        {
            var names = Directory.EnumerateFiles(_dataDir, "*" + JsonDatabaseStorage.FileExtension)
                .Select(f => Path.GetFileName(f)[..^JsonDatabaseStorage.FileExtension.Length])
                .Concat(_open.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.CurrentCulture);
            return names.Select(n => new DatabaseInfo(
                _open.TryGetValue(n, out var o) ? o.Database.Name : n,
                _open.TryGetValue(n, out var d) && d.IsDirty,
                File.Exists(PathFor(n)))).ToList();
        }
    }

    public Database Create(string name)
    {
        lock (_sync)
        {
            var database = new Database(name);
            CheckFileName(database.Name);
            if (_open.ContainsKey(database.Name) || File.Exists(PathFor(database.Name)))
                throw new ValidationException($"База «{database.Name}» вже існує.");
            _open[database.Name] = new OpenDatabase(database) { IsDirty = true };
            return database;
        }
    }

    public T Use<T>(string name, bool modifies, Func<Database, T> action)
    {
        lock (_sync)
        {
            var open = GetOpen(name);
            var result = action(open.Database);
            if (modifies)
                open.IsDirty = true;
            return result;
        }
    }

    public bool IsDirty(string name)
    {
        lock (_sync)
            return GetOpen(name).IsDirty;
    }

    public void Save(string name)
    {
        lock (_sync)
        {
            var open = GetOpen(name);
            _storage.Save(open.Database, PathFor(open.Database.Name));
            open.IsDirty = false;
        }
    }

    public void Reload(string name)
    {
        lock (_sync)
        {
            var path = PathFor(name);
            if (!File.Exists(path))
                throw new ValidationException($"База «{name}» ще не збережена на диску.");
            _open[name] = new OpenDatabase(_storage.Load(path));
        }
    }

    public string Export(string name)
    {
        lock (_sync)
            return JsonDatabaseStorage.Serialize(GetOpen(name).Database);
    }

    public Database Import(string json)
    {
        var database = JsonDatabaseStorage.Deserialize(json);
        lock (_sync)
        {
            CheckFileName(database.Name);
            if (_open.ContainsKey(database.Name) || File.Exists(PathFor(database.Name)))
                throw new ValidationException($"База «{database.Name}» вже існує — перейменуйте її у файлі або видаліть існуючу.");
            _open[database.Name] = new OpenDatabase(database) { IsDirty = true };
            return database;
        }
    }

    private OpenDatabase GetOpen(string name)
    {
        if (_open.TryGetValue(name, out var open))
            return open;
        var path = PathFor(name);
        if (!File.Exists(path))
            throw new NotFoundException($"Базу «{name}» не знайдено.");
        open = new OpenDatabase(_storage.Load(path));
        _open[name] = open;
        return open;
    }

    private string PathFor(string name)
    {
        CheckFileName(name);
        return Path.Combine(_dataDir, name + JsonDatabaseStorage.FileExtension);
    }

    private static void CheckFileName(string name)
    {
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name is "." or "..")
            throw new ValidationException("Назва бази містить недопустимі символи (\\ / : * ? \" < > |).");
    }
}

public record DatabaseInfo(string Name, bool IsDirty, bool IsSaved);
