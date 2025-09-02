using System.Text.RegularExpressions;

namespace SqliteWebDemoApi.Utilities;

/// <summary>
/// Utilities for working with SQLite identifiers (object names like tables, views, or columns).
/// 
/// Why this is needed:
/// - When building SQL dynamically, identifiers must be validated and safely quoted.
/// - Without validation, a malicious or malformed identifier could lead to SQL injection,
///   e.g. someone passing `"Users; DROP TABLE Orders; --"`.
/// - By restricting identifiers to a safe pattern (letters, digits, underscore) and
///   then quoting them correctly, we prevent injection and syntax errors while still
///   allowing legitimate names like `Users` or `Order_Items`.
/// </summary>
internal static class SqliteIdentifiers
{
    // Only letters, digits, and underscore allowed — matches your original rule.
    private static readonly Regex IdentifierRegex =
        new(@"^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    /// <summary>
    /// Ensures that the identifier is valid, otherwise throws an ArgumentException.
    /// Useful at API/service boundaries to fail fast.
    /// </summary>
    public static void EnsureValid(string identifier, string paramName)
    {
        if (!IsValid(identifier))
        {
            throw new ArgumentException("Invalid identifier.", paramName);
        }
    }

    /// <summary>
    /// Returns true if the identifier matches the safe pattern.
    /// </summary>
    private static bool IsValid(string identifier) =>
        !string.IsNullOrWhiteSpace(identifier) &&
        IdentifierRegex.IsMatch(identifier);

    /// <summary>
    /// Wraps an identifier in double quotes and escapes embedded quotes.
    /// This ensures that names like "User" or "Order_Items" are safe to use in SQL.
    /// Call this only on identifiers that are trusted or validated first.
    /// </summary>
    public static string Quote(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}