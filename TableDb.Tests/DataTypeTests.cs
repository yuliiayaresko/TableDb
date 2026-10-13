using TableDb.Core;

namespace TableDb.Tests;

public class DataTypeTests
{
    [Theory]
    [InlineData("student@knu.ua", true)]
    [InlineData("ivan.petrenko+lab@gmail.com", true)]
    [InlineData("a@b.co", true)]
    [InlineData("no-at-sign.com", false)]
    [InlineData("two@@example.com", false)]
    [InlineData("name@domain", false)]
    [InlineData("name..dots@example.com", false)]
    [InlineData(".start@example.com", false)]
    public void EmailType_ValidatesAddresses(string value, bool expected)
    {
        Assert.Equal(expected, new EmailType().IsValid(value));
    }

    [Fact]
    public void EnumType_AcceptsOnlyValuesFromSet()
    {
        var type = new EnumType(["денна", "заочна"]);

        Assert.True(type.IsValid("денна"));
        Assert.False(type.IsValid("вечірня"));
        Assert.False(type.IsValid("Денна"));
    }

    [Theory]
    [InlineData(FieldType.Integer, "42", "42")]
    [InlineData(FieldType.Integer, " -7 ", "-7")]
    [InlineData(FieldType.Real, "3,5", "3.5")]
    [InlineData(FieldType.Real, "1e3", "1000")]
    [InlineData(FieldType.Char, "Ж", "Ж")]
    [InlineData(FieldType.String, "будь-що", "будь-що")]
    public void BaseTypes_NormalizeValidValues(FieldType kind, string raw, string expected)
    {
        var type = DataTypeFactory.Create(kind);

        Assert.True(type.IsValid(raw));
        Assert.Equal(expected, type.Normalize(raw));
    }

    [Theory]
    [InlineData(FieldType.Integer, "4.5")]
    [InlineData(FieldType.Integer, "abc")]
    [InlineData(FieldType.Real, "1,2,3")]
    [InlineData(FieldType.Char, "ab")]
    public void BaseTypes_RejectInvalidValues(FieldType kind, string raw)
    {
        Assert.False(DataTypeFactory.Create(kind).IsValid(raw));
    }
}
