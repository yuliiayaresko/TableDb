using System.Text;
using System.Text.RegularExpressions;

namespace TableDb.Core;

public class SearchPattern
{
    private readonly Regex _regex;

    public SearchPattern(string pattern, bool caseSensitive = false)
    {
        if (string.IsNullOrEmpty(pattern))
            throw new ValidationException("Шаблон пошуку не може бути порожнім.");
        Pattern = pattern;
        CaseSensitive = caseSensitive;
        var options = RegexOptions.CultureInvariant | RegexOptions.Singleline;
        if (!caseSensitive)
            options |= RegexOptions.IgnoreCase;
        _regex = new Regex(ToRegex(pattern), options, TimeSpan.FromSeconds(1));
    }

    public string Pattern { get; }

    public bool CaseSensitive { get; }

    public bool IsMatch(string? value) => _regex.IsMatch(value ?? "");

    private static string ToRegex(string pattern)
    {
        var sb = new StringBuilder("^");
        foreach (var ch in pattern)
        {
            sb.Append(ch switch
            {
                '*' => ".*",
                '?' => ".",
                _ => Regex.Escape(ch.ToString())
            });
        }
        return sb.Append('$').ToString();
    }
}

public static class SearchService
{
    public static IReadOnlyList<SearchResult> Search(Table table, string pattern, string? columnName = null, bool caseSensitive = false)
    {
        ArgumentNullException.ThrowIfNull(table);
        var searchPattern = new SearchPattern(pattern, caseSensitive);
        return table.Search(searchPattern, columnName);
    }
}
