using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

// ConnectionStrings:Default should be "Data Source=example.db" (or an absolute path)
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Default");

// ---- CORS: allow any origin (dev) ----
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();
app.UseCors("AllowAll");

// ---------- Helpers ----------
static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";
static bool IsValidIdentifier(string id) => Regex.IsMatch(id, @"^[A-Za-z0-9_]+$");

static async Task<string[]> GetColumnNamesAsync(SqliteConnection conn, string quotedName, CancellationToken ct)
{
    // Use a LIMIT 0 reader to infer column names without loading data
    var cols = new List<string>();
    await using var cmd = new SqliteCommand($"SELECT * FROM {quotedName} LIMIT 0;", conn);
    await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SchemaOnly, ct);
    for (int i = 0; i < reader.FieldCount; i++)
        cols.Add(reader.GetName(i));
    return cols.ToArray();
}

// ---------- LIST: /api/tables ----------
app.MapGet("/api/tables", async (CancellationToken ct) =>
{
    await using var conn = new SqliteConnection(connectionString);
    await conn.OpenAsync(ct);

    var results = new List<object>();

    const string tablesSql = @"
SELECT name, sql
FROM sqlite_master
WHERE type = 'table'
  AND name NOT LIKE 'sqlite_%'
ORDER BY name;";

    await using (var cmd = new SqliteCommand(tablesSql, conn))
    await using (var r = await cmd.ExecuteReaderAsync(ct))
    {
        while (await r.ReadAsync(ct))
        {
            var name = r.GetString(0);
            var quoted = Quote(name);

            // Row count (best-effort; lightweight for small/med tables)
            long rowCount = 0;
            try
            {
                await using var c2 = new SqliteCommand($"SELECT COUNT(*) FROM {quoted};", conn);
                rowCount = (long)(await c2.ExecuteScalarAsync(ct) ?? 0L);
            }
            catch { /* view or virtual table mismatches, ignore */ }

            // Column names
            string[] columns;
            try { columns = await GetColumnNamesAsync(conn, quoted, ct); }
            catch { columns = Array.Empty<string>(); }

            results.Add(new
            {
                name,
                rowCount,
                columns
            });
        }
    }

    return Results.Ok(new { items = results, total = results.Count });
});

// ---------- LIST: /api/views ----------
app.MapGet("/api/views", async (CancellationToken ct) =>
{
    await using var conn = new SqliteConnection(connectionString);
    await conn.OpenAsync(ct);

    var results = new List<object>();

    const string viewsSql = @"
SELECT name, sql
FROM sqlite_master
WHERE type = 'view'
ORDER BY name;";

    await using (var cmd = new SqliteCommand(viewsSql, conn))
    await using (var r = await cmd.ExecuteReaderAsync(ct))
    {
        while (await r.ReadAsync(ct))
        {
            var name = r.GetString(0);
            var quoted = Quote(name);

            // Column names (best-effort)
            string[] columns;
            try { columns = await GetColumnNamesAsync(conn, quoted, ct); }
            catch { columns = Array.Empty<string>(); }

            results.Add(new
            {
                name,
                columns
            });
        }
    }

    return Results.Ok(new { items = results, total = results.Count });
});

// ---------- DATA: /api/tables/{tableId} ----------
app.MapGet("/api/tables/{tableId}", async (
    string tableId,
    int page = 1,
    int pageSize = 50,
    CancellationToken ct = default) =>
{
    if (string.IsNullOrWhiteSpace(tableId) || !IsValidIdentifier(tableId))
        return Results.BadRequest("Invalid table name.");

    if (page < 1) page = 1;
    if (pageSize < 1 || pageSize > 1000) pageSize = Math.Clamp(pageSize, 1, 1000);

    await using var conn = new SqliteConnection(connectionString);
    await conn.OpenAsync(ct);

    // Ensure table exists
    const string tableExistsSql = @"SELECT 1 FROM sqlite_master WHERE type='table' AND name=@name;";
    await using (var existsCmd = new SqliteCommand(tableExistsSql, conn))
    {
        existsCmd.Parameters.AddWithValue("@name", tableId);
        var exists = await existsCmd.ExecuteScalarAsync(ct);
        if (exists is null) return Results.NotFound($"Table \"{tableId}\" not found.");
    }

    // Count rows
    var quoted = Quote(tableId);
    long totalRows;
    await using (var countCmd = new SqliteCommand($"SELECT COUNT(*) FROM {quoted};", conn))
        totalRows = (long)(await countCmd.ExecuteScalarAsync(ct) ?? 0L);

    var totalPages = (int)Math.Max(1, Math.Ceiling(totalRows / (double)pageSize));
    if (totalRows == 0)
        return Results.Ok(new { type = "table", name = tableId, page, pageSize, totalRows, totalPages, data = Array.Empty<object>() });

    if (page > totalPages) page = totalPages;
    var offset = (page - 1) * pageSize;

    // Use rowid ordering only if NOT WITHOUT ROWID
    var withoutRowId = false;
    const string checkWithoutRowId = @"SELECT instr(lower(sql),'without rowid') FROM sqlite_master WHERE type='table' AND name=@name;";
    await using (var chk = new SqliteCommand(checkWithoutRowId, conn))
    {
        chk.Parameters.AddWithValue("@name", tableId);
        var v = await chk.ExecuteScalarAsync(ct);
        withoutRowId = (v is long n && n > 0);
    }
    var orderBy = withoutRowId ? "" : "ORDER BY rowid";

    var dataSql = $@"
SELECT *
FROM {quoted}
{orderBy}
LIMIT @take OFFSET @offset;";

    var rows = new List<Dictionary<string, object?>>(pageSize);
    await using (var cmd = new SqliteCommand(dataSql, conn))
    {
        cmd.Parameters.AddWithValue("@take", pageSize);
        cmd.Parameters.AddWithValue("@offset", offset);

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
        var fieldCount = reader.FieldCount;
        var names = new string[fieldCount];
        for (int i = 0; i < fieldCount; i++) names[i] = reader.GetName(i);

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

    return Results.Ok(new { type = "table", name = tableId, page, pageSize, totalRows, totalPages, data = rows });
});

// ---------- DATA: /api/views/{viewId} ----------
app.MapGet("/api/views/{viewId}", async (
    string viewId,
    int page = 1,
    int pageSize = 50,
    CancellationToken ct = default) =>
{
    if (string.IsNullOrWhiteSpace(viewId) || !IsValidIdentifier(viewId))
        return Results.BadRequest("Invalid view name.");

    if (page < 1) page = 1;
    if (pageSize < 1 || pageSize > 1000) pageSize = Math.Clamp(pageSize, 1, 1000);

    await using var conn = new SqliteConnection(connectionString);
    await conn.OpenAsync(ct);

    // Ensure view exists
    const string viewExistsSql = @"SELECT 1 FROM sqlite_master WHERE type='view' AND name=@name;";
    await using (var existsCmd = new SqliteCommand(viewExistsSql, conn))
    {
        existsCmd.Parameters.AddWithValue("@name", viewId);
        var exists = await existsCmd.ExecuteScalarAsync(ct);
        if (exists is null) return Results.NotFound($"View \"{viewId}\" not found.");
    }

    var quoted = Quote(viewId);

    // Count rows in the view
    long totalRows;
    await using (var countCmd = new SqliteCommand($"SELECT COUNT(*) FROM {quoted};", conn))
        totalRows = (long)(await countCmd.ExecuteScalarAsync(ct) ?? 0L);

    var totalPages = (int)Math.Max(1, Math.Ceiling(totalRows / (double)pageSize));
    if (totalRows == 0)
        return Results.Ok(new { type = "view", name = viewId, page, pageSize, totalRows, totalPages, data = Array.Empty<object>() });

    if (page > totalPages) page = totalPages;
    var offset = (page - 1) * pageSize;

    // Views don’t have rowid; just paginate with LIMIT/OFFSET
    var dataSql = $@"
SELECT *
FROM {quoted}
LIMIT @take OFFSET @offset;";

    var rows = new List<Dictionary<string, object?>>(pageSize);
    await using (var cmd = new SqliteCommand(dataSql, conn))
    {
        cmd.Parameters.AddWithValue("@take", pageSize);
        cmd.Parameters.AddWithValue("@offset", offset);

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
        var fieldCount = reader.FieldCount;
        var names = new string[fieldCount];
        for (int i = 0; i < fieldCount; i++) names[i] = reader.GetName(i);

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

    return Results.Ok(new { type = "view", name = viewId, page, pageSize, totalRows, totalPages, data = rows });
});

app.Run();
