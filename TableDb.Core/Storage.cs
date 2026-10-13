using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TableDb.Core;

public interface IDatabaseStorage
{
    void Save(Database database, string path);

    Database Load(string path);
}

public class JsonDatabaseStorage : IDatabaseStorage
{
    public const string FileExtension = ".tdb.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public void Save(Database database, string path)
    {
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, Serialize(database));
        File.Move(tempPath, path, overwrite: true);
    }

    public Database Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Файл бази не знайдено.", path);
        return Deserialize(File.ReadAllText(path));
    }

    public static string Serialize(Database database) => JsonSerializer.Serialize(database, Options);

    public static Database Deserialize(string json)
    {
        Database? database;
        try
        {
            database = JsonSerializer.Deserialize<Database>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new ValidationException("Файл бази пошкоджений або має невірний формат: " + ex.Message);
        }
        if (database is null || string.IsNullOrWhiteSpace(database.Name))
            throw new ValidationException("Файл не містить коректної бази даних.");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in database.Tables)
        {
            table.ValidateSchema();
            if (!names.Add(table.Name))
                throw new ValidationException($"Таблиця «{table.Name}» повторюється у файлі.");
            for (var i = 0; i < table.Rows.Count; i++)
                table.Rows[i] = table.CreateValidatedRow(table.Rows[i].Values);
        }
        return database;
    }
}
