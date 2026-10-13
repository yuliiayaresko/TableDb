using System.Globalization;
using System.Text.RegularExpressions;

namespace TableDb.Core;

public enum FieldType
{
    Integer,
    Real,
    Char,
    String,
    Enum,
    Email
}

public abstract class DataType
{
    public abstract FieldType Kind { get; }

    public abstract string DisplayName { get; }

    public abstract bool IsValid(string value);

    public virtual string Normalize(string value) => value;

    public abstract string FormatHint { get; }
}

public sealed class IntegerType : DataType
{
    public override FieldType Kind => FieldType.Integer;
    public override string DisplayName => "integer";
    public override string FormatHint => "ціле число, наприклад 42 або -7";

    public override bool IsValid(string value) =>
        long.TryParse(value.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _);

    public override string Normalize(string value) =>
        long.Parse(value.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)
            .ToString(CultureInfo.InvariantCulture);
}

public sealed class RealType : DataType
{
    private const NumberStyles Styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

    public override FieldType Kind => FieldType.Real;
    public override string DisplayName => "real";
    public override string FormatHint => "дійсне число, наприклад 3.14 або 3,14";

    public override bool IsValid(string value) =>
        double.TryParse(Prepare(value), Styles, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d);

    public override string Normalize(string value) =>
        double.Parse(Prepare(value), Styles, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture);

    private static string Prepare(string value) => value.Trim().Replace(',', '.');
}

public sealed class CharType : DataType
{
    public override FieldType Kind => FieldType.Char;
    public override string DisplayName => "char";
    public override string FormatHint => "рівно один символ";

    public override bool IsValid(string value) => value.Length == 1;
}

public sealed class StringType : DataType
{
    public override FieldType Kind => FieldType.String;
    public override string DisplayName => "string";
    public override string FormatHint => "довільний рядок";

    public override bool IsValid(string value) => true;
}

public sealed class EnumType : DataType
{
    private readonly List<string> _allowed;

    public EnumType(IEnumerable<string> allowedValues)
    {
        _allowed = allowedValues.ToList();
    }

    public IReadOnlyList<string> AllowedValues => _allowed;

    public override FieldType Kind => FieldType.Enum;
    public override string DisplayName => "enum";
    public override string FormatHint => "одне зі значень: " + string.Join(", ", _allowed);

    public override bool IsValid(string value) => _allowed.Contains(value, StringComparer.Ordinal);
}

public sealed partial class EmailType : DataType
{
    public override FieldType Kind => FieldType.Email;
    public override string DisplayName => "email";
    public override string FormatHint => "адреса e-mail, наприклад name@example.com";

    public override bool IsValid(string value)
    {
        var v = value.Trim();
        return v.Length <= 254 && !v.Contains("..") && EmailRegex().IsMatch(v);
    }

    public override string Normalize(string value) => value.Trim();

    [GeneratedRegex(@"^[A-Za-z0-9](?:[A-Za-z0-9._%+-]*[A-Za-z0-9])?@[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)*\.[A-Za-z]{2,}$")]
    private static partial Regex EmailRegex();
}

public static class DataTypeFactory
{
    public static DataType Create(FieldType kind, IEnumerable<string>? enumValues = null) => kind switch
    {
        FieldType.Integer => new IntegerType(),
        FieldType.Real => new RealType(),
        FieldType.Char => new CharType(),
        FieldType.String => new StringType(),
        FieldType.Enum => new EnumType(enumValues ?? []),
        FieldType.Email => new EmailType(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
