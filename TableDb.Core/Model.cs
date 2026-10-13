using System.Text.Json.Serialization;

namespace TableDb.Core;

public class ValidationException(string message) : Exception(message);

public class Column
{
    public Column()
    {
    }

    public Column(string name, FieldType type, IEnumerable<string>? enumValues = null)
    {
        Name = name;
        Type = type;
        EnumValues = enumValues?.ToList() ?? [];
    }

    public string Name { get; set; } = "";

    public FieldType Type { get; set; }

    public List<string> EnumValues { get; set; } = [];

    [JsonIgnore]
    public DataType DataType => DataTypeFactory.Create(Type, EnumValues);

    public string? ValidateValue(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return null;
        var type = DataType;
        if (!type.IsValid(raw))
            throw new ValidationException($"Поле «{Name}» ({type.DisplayName}): значення «{raw}» некоректне — очікується {type.FormatHint}.");
        return type.Normalize(raw);
    }

    public override string ToString() => Type == FieldType.Enum
        ? $"{Name}: enum{{{string.Join(", ", EnumValues)}}}"
        : $"{Name}: {DataTypeFactory.Create(Type).DisplayName}";
}

public class Row
{
    public Row()
    {
    }

    public Row(IEnumerable<string?> values)
    {
        Values = values.ToList();
    }

    public List<string?> Values { get; set; } = [];

    public string? this[int index] => Values[index];
}

public record SearchResult(int RowIndex, Row Row);
