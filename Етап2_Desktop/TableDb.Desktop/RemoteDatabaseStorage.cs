using System.Net.Http;
using System.Text;
using System.Text.Json;
using TableDb.Core;

namespace TableDb.Desktop;

public sealed class RemoteDatabaseStorage : IDatabaseStorage, IDisposable
{
    private const string ApiPath = "api/databases";

    private readonly HttpClient _http;

    public RemoteDatabaseStorage(string baseUrl)
    {
        var url = (baseUrl ?? "").Trim();
        if (url.Length == 0)
            throw new ValidationException("Адреса сервера не може бути порожньою.");
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = "http://" + url;
        if (!url.EndsWith('/'))
            url += "/";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ValidationException($"«{baseUrl}» не є коректною адресою сервера.");

        _http = new HttpClient { BaseAddress = uri, Timeout = TimeSpan.FromSeconds(20) };
        ServerUrl = uri.ToString();
    }

    public string ServerUrl { get; }

    public IReadOnlyList<string> ListDatabases()
    {
        using var document = JsonDocument.Parse(Request(HttpMethod.Get, ApiPath));
        return document.RootElement.EnumerateArray()
            .Select(e => e.TryGetProperty("name", out var n) ? n.GetString() : null)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToList();
    }

    public Database Load(string name) =>
        JsonDatabaseStorage.Deserialize(Request(HttpMethod.Get, $"{ApiPath}/{Uri.EscapeDataString(name)}/export"));

    public void Save(Database database, string name)
    {
        ArgumentNullException.ThrowIfNull(database);
        Request(HttpMethod.Put, $"{ApiPath}/{Uri.EscapeDataString(name)}", JsonDatabaseStorage.Serialize(database));
    }

    private string Request(HttpMethod method, string path, string? body = null) =>
        Task.Run(() => SendAsync(method, path, body)).GetAwaiter().GetResult();

    private async Task<string> SendAsync(HttpMethod method, string path, string? body)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new IOException($"Немає зв'язку із сервером {ServerUrl}. Перевірте, чи він запущений.", ex);
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
                return text;
            throw new ValidationException(ExtractError(text) ?? $"Сервер відповів кодом {(int)response.StatusCode}.");
        }
    }

    private static string? ExtractError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("error", out var error))
                return error.GetString();
        }
        catch (JsonException)
        {
        }
        return null;
    }

    public void Dispose() => _http.Dispose();
}
