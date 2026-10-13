using TableDb.Core;

namespace TableDb.Tests;

public class DatabaseTests
{
    [Fact]
    public void CreateTable_WithDuplicateColumns_Throws()
    {
        var db = new Database("Test");

        var ex = Assert.Throws<ValidationException>(() => db.CreateTable("T",
        [
            new Column("Name", FieldType.String),
            new Column("name", FieldType.Integer)
        ]));
        Assert.Contains("повторюється", ex.Message);
    }

    [Fact]
    public void CreateTable_EnumWithoutValues_Throws()
    {
        var db = new Database("Test");

        Assert.Throws<ValidationException>(() => db.CreateTable("T", [new Column("Status", FieldType.Enum)]));
    }

    [Fact]
    public void CreateTable_WithExistingName_Throws()
    {
        var db = new Database("Test");
        db.CreateTable("Books", [new Column("Title", FieldType.String)]);

        Assert.Throws<ValidationException>(() => db.CreateTable("books", [new Column("X", FieldType.Integer)]));
    }

    [Fact]
    public void DropTable_RemovesTable()
    {
        var db = new Database("Test");
        db.CreateTable("Books", [new Column("Title", FieldType.String)]);

        db.DropTable("Books");

        Assert.Empty(db.Tables);
    }

    [Fact]
    public void AddRow_WithInvalidValue_ThrowsAndDoesNotAddRow()
    {
        var db = new Database("Test");
        var table = db.CreateTable("People", [new Column("Age", FieldType.Integer), new Column("Mail", FieldType.Email)]);

        Assert.Throws<ValidationException>(() => table.AddRow(["двадцять", "a@b.com"]));
        Assert.Throws<ValidationException>(() => table.AddRow(["20", "not-an-email"]));
        Assert.Empty(table.Rows);
    }

    [Fact]
    public void UpdateRow_ValidatesAndReplacesValues()
    {
        var db = new Database("Test");
        var table = db.CreateTable("People", [new Column("Age", FieldType.Integer), new Column("Mail", FieldType.Email)]);
        table.AddRow(["20", "a@b.com"]);

        table.UpdateRow(0, ["21", ""]);

        Assert.Equal("21", table.Rows[0][0]);
        Assert.Null(table.Rows[0][1]);
        Assert.Throws<ValidationException>(() => table.UpdateRow(0, ["x", null]));
        Assert.Equal("21", table.Rows[0][0]);
    }

    [Fact]
    public void SaveAndLoad_PreservesSchemaAndData()
    {
        var db = new Database("Бібліотека");
        var table = db.CreateTable("Читачі",
        [
            new Column("Id", FieldType.Integer),
            new Column("Рейтинг", FieldType.Real),
            new Column("Клас", FieldType.Char),
            new Column("ПІБ", FieldType.String),
            new Column("Статус", FieldType.Enum, ["активний", "заблокований"]),
            new Column("Email", FieldType.Email)
        ]);
        table.AddRow(["1", "4.75", "A", "Шевченко Тарас", "активний", "taras@ukr.net"]);
        var path = Path.Combine(Path.GetTempPath(), $"tdb-test-{Guid.NewGuid()}{JsonDatabaseStorage.FileExtension}");
        var storage = new JsonDatabaseStorage();

        try
        {
            storage.Save(db, path);
            var loaded = storage.Load(path);

            var loadedTable = loaded.GetTable("Читачі");
            Assert.Equal("Бібліотека", loaded.Name);
            Assert.Equal(table.Columns.Select(c => c.ToString()), loadedTable.Columns.Select(c => c.ToString()));
            Assert.Equal(table.Rows[0].Values, loadedTable.Rows[0].Values);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_CorruptedData_Throws()
    {
        const string json = """
            { "Name": "X", "Tables": [ { "Name": "T",
              "Columns": [ { "Name": "Age", "Type": "Integer", "EnumValues": [] } ],
              "Rows": [ { "Values": [ "not a number" ] } ] } ] }
            """;

        Assert.Throws<ValidationException>(() => JsonDatabaseStorage.Deserialize(json));
    }
}
