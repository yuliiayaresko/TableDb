using TableDb.Core;

namespace TableDb.Tests;

public class SearchTests
{
    private static Table CreateStudents()
    {
        var db = new Database("Університет");
        var table = db.CreateTable("Студенти",
        [
            new Column("Id", FieldType.Integer),
            new Column("ПІБ", FieldType.String),
            new Column("Email", FieldType.Email),
            new Column("Курс", FieldType.Enum, ["1", "2", "3", "4"])
        ]);
        table.AddRow(["1", "Іваненко Іван", "ivan@gmail.com", "4"]);
        table.AddRow(["2", "Петренко Олена", "olena@ukr.net", "3"]);
        table.AddRow(["3", "Іванова Марія", "maria@gmail.com", "4"]);
        table.AddRow(["4", "Коваль Петро", "petro@knu.ua", "1"]);
        return table;
    }

    [Fact]
    public void Search_ByPatternInColumn_ReturnsOnlyMatchingRowsWithIndexes()
    {
        var table = CreateStudents();

        var results = SearchService.Search(table, "*@gmail.com", "Email");

        Assert.Equal([0, 2], results.Select(r => r.RowIndex));
        Assert.Equal("Іваненко Іван", results[0].Row[1]);
        Assert.Equal("Іванова Марія", results[1].Row[1]);
    }

    [Fact]
    public void Search_AcrossAllColumns_IsCaseInsensitiveByDefault()
    {
        var table = CreateStudents();

        var results = SearchService.Search(table, "іван*");

        Assert.Equal([0, 2], results.Select(r => r.RowIndex));
    }

    [Fact]
    public void Search_CaseSensitive_DoesNotMatchDifferentCase()
    {
        var table = CreateStudents();

        var results = SearchService.Search(table, "іван*", caseSensitive: true);

        Assert.Empty(results);
    }

    [Theory]
    [InlineData("Курс", "?", new[] { 0, 1, 2, 3 })]
    [InlineData("ПІБ", "* Петр?", new[] { 3 })]
    [InlineData("ПІБ", "Іван*", new[] { 0, 2 })]
    [InlineData("Email", "*.ua", new[] { 3 })]
    [InlineData("Email", "*[*", new int[0])]
    public void Search_WildcardsInSpecificColumn(string column, string pattern, int[] expectedRows)
    {
        var table = CreateStudents();

        var results = SearchService.Search(table, pattern, column);

        Assert.Equal(expectedRows, results.Select(r => r.RowIndex));
    }

    [Fact]
    public void Search_UnknownColumn_Throws()
    {
        var table = CreateStudents();

        Assert.Throws<ValidationException>(() => SearchService.Search(table, "*", "Телефон"));
    }

    [Fact]
    public void Search_EmptyPattern_Throws()
    {
        var table = CreateStudents();

        Assert.Throws<ValidationException>(() => SearchService.Search(table, ""));
    }
}
