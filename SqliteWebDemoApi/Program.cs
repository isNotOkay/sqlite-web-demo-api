using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

// ConnectionStrings:Default should be "Data Source=example.db" (or an absolute path)
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Default");

// ---- CORS: allow any origin ----
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

// ---- Enable CORS globally ----
app.UseCors("AllowAll");

// GET /data/{tableName}?page=1&pageSize=50
app.MapGet("/data/{tableName}", async (
    string tableName,
    int page = 1,
    int pageSize = 50,
    CancellationToken ct = default) =>
{
    if (string.IsNullOrWhiteSpace(tableName))
        return Results.BadRequest("tableName is required.");

    var identRegex = new Regex(@"^[A-Za-z0-9_]+$", RegexOptions.Compiled);
    if (!identRegex.IsMatch(tableName))
        return Results.BadRequest("Invalid table name.");

    if (page < 1) page = 1;
    if (pageSize < 1 || pageSize > 1000) pageSize = Math.Clamp(pageSize, 1, 1000);

    await using var conn = new SqliteConnection(connectionString);
    await conn.OpenAsync(ct);

    const string existsSql = @"SELECT 1 FROM sqlite_master WHERE type='table' AND name=@name;";
    await using (var existsCmd = new SqliteCommand(existsSql, conn))
    {
        existsCmd.Parameters.AddWithValue("@name", tableName);
        var exists = await existsCmd.ExecuteScalarAsync(ct);
        if (exists is null)
            return Results.NotFound($"Table \"{tableName}\" not found.");
    }

    var quoted = Quote(tableName);
    var countSql = $"SELECT COUNT(*) FROM {quoted};";
    long totalRows;
    await using (var countCmd = new SqliteCommand(countSql, conn))
    {
        totalRows = (long)(await countCmd.ExecuteScalarAsync(ct) ?? 0L);
    }

    var totalPages = (int)Math.Max(1, Math.Ceiling(totalRows / (double)pageSize));
    if (totalRows == 0)
    {
        return Results.Ok(new
        {
            table = tableName,
            page,
            pageSize,
            totalRows,
            totalPages,
            data = Array.Empty<object>()
        });
    }

    if (page > totalPages) page = totalPages;
    var offset = (page - 1) * pageSize;

    var dataSql = $@"
SELECT *
FROM {quoted}
ORDER BY rowid
LIMIT @take OFFSET @offset;";

    var rows = new List<Dictionary<string, object?>>(pageSize);

    await using (var cmd = new SqliteCommand(dataSql, conn))
    {
        cmd.Parameters.AddWithValue("@take", pageSize);
        cmd.Parameters.AddWithValue("@offset", offset);

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);

        var fieldCount = reader.FieldCount;
        var names = new string[fieldCount];
        for (int i = 0; i < fieldCount; i++)
            names[i] = reader.GetName(i);

        while (await reader.ReadAsync(ct))
        {
            var dict = new Dictionary<string, object?>(fieldCount, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < fieldCount; i++)
            {
                var val = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
                if (val is byte[] bytes) val = Convert.ToBase64String(bytes);
                dict[names[i]] = val;
            }
            rows.Add(dict);
        }
    }

    return Results.Ok(new
    {
        table = tableName,
        page,
        pageSize,
        totalRows,
        totalPages,
        data = rows
    });
});

app.Run();

// Quote identifiers for SQLite: "identifier" (double any embedded ")
static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";
