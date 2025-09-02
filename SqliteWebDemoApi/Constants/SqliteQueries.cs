using System.Text;

namespace SqliteWebDemoApi.Constants;

public static class SqliteQueries
{
    public const string ListTables = """

                                     SELECT name, sql
                                     FROM sqlite_master
                                     WHERE type = 'table'
                                       AND name NOT LIKE 'sqlite_%'
                                     ORDER BY name;
                                     """;

    public const string ListViews = """

                                    SELECT name, sql
                                    FROM sqlite_master
                                    WHERE type = 'view'
                                    ORDER BY name;
                                    """;

    public const string ObjectExists = """

                                       SELECT 1
                                       FROM sqlite_master
                                       WHERE type = @type AND name = @name;
                                       """;

    public const string CheckWithoutRowId = """

                                            SELECT instr(lower(sql), 'without rowid')
                                            FROM sqlite_master
                                            WHERE type = 'table' AND name = @name;
                                            """;

    public static string CountAll(string quotedName) =>
        $"SELECT COUNT(*) FROM {quotedName};";

    public static string SelectSchemaOnly(string quotedName) =>
        $"SELECT * FROM {quotedName} LIMIT 0;";

    public static string SelectPage(string quotedName, bool orderByRowId) =>
        orderByRowId
            ? $"SELECT * FROM {quotedName} ORDER BY rowid LIMIT @take OFFSET @offset;"
            : $"SELECT * FROM {quotedName} LIMIT @take OFFSET @offset;";

    public static string SelectPage(string quotedName, string? orderByColumn, bool orderByDesc, bool addRowIdTiebreaker)
    {
        // Base select
        var sb = new StringBuilder()
            .Append("SELECT * FROM ").Append(quotedName);

        // ORDER BY (safe: identifiers were validated & quoted earlier)
        if (!string.IsNullOrEmpty(orderByColumn))
        {
            sb.Append(" ORDER BY ")
                .Append(orderByColumn)
                .Append(orderByDesc ? " DESC" : " ASC");

            if (addRowIdTiebreaker)
                sb.Append(", rowid"); // stable paging when values tie
        }
        else if (addRowIdTiebreaker)
        {
            sb.Append(" ORDER BY rowid");
        }

        sb.Append(" LIMIT @take OFFSET @offset");
        return sb.ToString();
    }
}