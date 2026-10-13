using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TableDb.Core;
using TableDb.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    o.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});

var dataDir = Path.Combine(builder.Environment.ContentRootPath, "Data");
SeedSampleData(dataDir, Path.Combine(AppContext.BaseDirectory, "SampleData"));
builder.Services.AddSingleton(new DatabaseService(dataDir));

var app = builder.Build();

app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (Exception ex) when (ex is ValidationException or NotFoundException or BadHttpRequestException)
    {
        context.Response.StatusCode = ex is NotFoundException ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest;
        var message = ex is BadHttpRequestException ? "Некоректний запит: " + ex.Message : ex.Message;
        await context.Response.WriteAsJsonAsync(new { error = message });
    }
});

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api/databases");

api.MapGet("/", (DatabaseService s) => s.List());

api.MapPost("/", (CreateDatabaseRequest req, DatabaseService s) =>
{
    var db = s.Create(req.Name ?? "");
    return Results.Created($"/api/databases/{Uri.EscapeDataString(db.Name)}", DatabaseDto.From(db, true));
});

api.MapGet("/{db}", (string db, DatabaseService s) =>
    s.Use(db, false, d => DatabaseDto.From(d, s.IsDirty(db))));

api.MapPost("/{db}/save", (string db, DatabaseService s) =>
{
    s.Save(db);
    return Results.Ok(new { message = $"Базу «{db}» збережено на диску сервера." });
});

api.MapPost("/{db}/reload", (string db, DatabaseService s) =>
{
    s.Reload(db);
    return Results.Ok(new { message = $"Базу «{db}» перечитано з диска." });
});

api.MapGet("/{db}/export", (string db, DatabaseService s) =>
    Results.File(Encoding.UTF8.GetBytes(s.Export(db)), "application/json", db + JsonDatabaseStorage.FileExtension));

api.MapPost("/import", async (HttpRequest request, DatabaseService s) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var db = s.Import(await reader.ReadToEndAsync());
    return Results.Created($"/api/databases/{Uri.EscapeDataString(db.Name)}", DatabaseDto.From(db, true));
});

api.MapPost("/{db}/tables", (string db, CreateTableRequest req, DatabaseService s) =>
{
    var columns = (req.Columns ?? []).Select(c => new Column(c.Name ?? "", c.Type, c.EnumValues ?? []));
    var table = s.Use(db, true, d => d.CreateTable(req.Name ?? "", columns));
    return Results.Created($"/api/databases/{Uri.EscapeDataString(db)}/tables/{Uri.EscapeDataString(table.Name)}", TableDto.From(table));
});

api.MapDelete("/{db}/tables/{table}", (string db, string table, DatabaseService s) =>
{
    s.Use(db, true, d => { d.DropTable(table); return true; });
    return Results.NoContent();
});

api.MapGet("/{db}/tables/{table}", (string db, string table, DatabaseService s) =>
    s.Use(db, false, d => TableDto.From(d.GetTable(table))));

api.MapPost("/{db}/tables/{table}/rows", (string db, string table, RowRequest req, DatabaseService s) =>
    s.Use(db, true, d =>
    {
        var t = d.GetTable(table);
        var row = t.AddRow(req.Values ?? []);
        return Results.Ok(new RowDto(t.Rows.Count - 1, row.Values));
    }));

api.MapPut("/{db}/tables/{table}/rows/{index:int}", (string db, string table, int index, RowRequest req, DatabaseService s) =>
    s.Use(db, true, d =>
    {
        var t = d.GetTable(table);
        t.UpdateRow(index, req.Values ?? []);
        return Results.Ok(new RowDto(index, t.Rows[index].Values));
    }));

api.MapDelete("/{db}/tables/{table}/rows/{index:int}", (string db, string table, int index, DatabaseService s) =>
{
    s.Use(db, true, d => { d.GetTable(table).DeleteRow(index); return true; });
    return Results.NoContent();
});

api.MapGet("/{db}/tables/{table}/search", (string db, string table, string pattern, string? column, bool? caseSensitive, DatabaseService s) =>
    s.Use(db, false, d =>
    {
        var t = d.GetTable(table);
        var results = SearchService.Search(t, pattern, string.IsNullOrEmpty(column) ? null : column, caseSensitive ?? false);
        return new SearchResponse(pattern, column, caseSensitive ?? false, t.Rows.Count,
            results.Select(r => new RowDto(r.RowIndex, r.Row.Values)).ToList());
    }));

app.Run();

static void SeedSampleData(string dataDir, string sampleDir)
{
    Directory.CreateDirectory(dataDir);
    if (Directory.EnumerateFiles(dataDir).Any() || !Directory.Exists(sampleDir))
        return;
    foreach (var file in Directory.EnumerateFiles(sampleDir))
        File.Copy(file, Path.Combine(dataDir, Path.GetFileName(file)));
}

record CreateDatabaseRequest(string? Name);
record ColumnRequest(string? Name, FieldType Type, List<string>? EnumValues);
record CreateTableRequest(string? Name, List<ColumnRequest>? Columns);
record RowRequest(List<string?>? Values);
record RowDto(int Index, List<string?> Values);
record SearchResponse(string Pattern, string? Column, bool CaseSensitive, int TotalRows, List<RowDto> Results);

record TableSummaryDto(string Name, List<Column> Columns, int RowCount);

record DatabaseDto(string Name, bool IsDirty, List<TableSummaryDto> Tables)
{
    public static DatabaseDto From(Database d, bool isDirty) =>
        new(d.Name, isDirty, d.Tables.Select(t => new TableSummaryDto(t.Name, t.Columns, t.Rows.Count)).ToList());
}

record TableDto(string Name, List<Column> Columns, List<RowDto> Rows)
{
    public static TableDto From(Table t) =>
        new(t.Name, t.Columns, t.Rows.Select((r, i) => new RowDto(i, r.Values)).ToList());
}
