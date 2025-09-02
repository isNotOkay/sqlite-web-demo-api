using SqliteWebDemoApi.Utilities;

namespace SqliteWebDemoApiTest;

public sealed class SqliteIdentifiersTests
{
    [Theory]
    [InlineData("Users")]
    [InlineData("Order_Items")]
    [InlineData("A123")]
    [InlineData("x")]
    public void EnsureValid_AllowsSafeIdentifiers(string identifier)
    {
        // Act + Assert (no exception expected)
        SqliteIdentifiers.EnsureValid(identifier, nameof(identifier));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Users; DROP TABLE Orders;--")]
    [InlineData("Order Items")] // space not allowed
    [InlineData("Name!")] // special char not allowed
    [InlineData("Table-1")] // dash not allowed
    [InlineData("äöü")] // non-ASCII not allowed
    public void EnsureValid_ThrowsForInvalidIdentifiers(string identifier)
    {
        // Act + Assert
        Assert.Throws<ArgumentException>(() => SqliteIdentifiers.EnsureValid(identifier, nameof(identifier)));
    }
    
    [Theory]
    [InlineData("Users", "\"Users\"")]
    [InlineData("Order_Items", "\"Order_Items\"")]
    [InlineData("A123", "\"A123\"")]
    [InlineData("x", "\"x\"")]
    public void Quote_WrapsInDoubleQuotes(string input, string expected)
    {
        var quoted = SqliteIdentifiers.Quote(input);
        Assert.Equal(expected, quoted);
    }

    [Fact]
    public void Quote_EscapesInternalQuotes()
    {
        const string input = "Some\"Name";
        var quoted = SqliteIdentifiers.Quote(input);
        Assert.Equal("\"Some\"\"Name\"", quoted);
    }
}