using Moq;
using SqliteWebDemoApi.Models;
using SqliteWebDemoApi.Repositories;
using SqliteWebDemoApi.Services;
using SqliteWebDemoApi.Utilities;

namespace SqliteWebDemoApiTest;

public sealed class SqliteBrowserTests
{
    private static SqliteBrowser CreateService(Mock<ISqliteRepository> repoMock) =>
        new(repoMock.Object);


    [Fact]
    public async Task ListTablesAsync_ReturnsItemsAndTotal_AndUsesTablesQuery()
    {
        // Arrange
        var items = new List<SqliteRelationInfo>
        {
            new() { Name = "t1", RowCount = 3, Columns = ["id", "name"] },
            new() { Name = "t2", RowCount = 0, Columns = ["x"] }
        };

        var repo = new Mock<ISqliteRepository>(MockBehavior.Strict);
        string? capturedSql = null;

        repo.Setup(r => r.ListRelationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((sql, _) => capturedSql = sql)
            .ReturnsAsync(items);

        var svc = CreateService(repo);

        // Act
        var (result, total) = await svc.ListTablesAsync(CancellationToken.None);

        // Assert
        Assert.Equal(items, result);
        Assert.Equal(items.Count, total);
        Assert.Equal(SqliteQueries.ListTables, capturedSql);
        repo.VerifyAll();
    }

    [Fact]
    public async Task ListViewsAsync_ReturnsItemsAndTotal_AndUsesViewsQuery()
    {
        // Arrange
        var items = new List<SqliteRelationInfo>
        {
            new() { Name = "v1", RowCount = 10, Columns = ["a"] }
        };

        var repo = new Mock<ISqliteRepository>(MockBehavior.Strict);
        string? capturedSql = null;

        repo.Setup(r => r.ListRelationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((sql, _) => capturedSql = sql)
            .ReturnsAsync(items);

        var svc = CreateService(repo);

        // Act
        var (result, total) = await svc.ListViewsAsync(CancellationToken.None);

        // Assert
        Assert.Equal(items, result);
        Assert.Equal(items.Count, total);
        Assert.Equal(SqliteQueries.ListViews, capturedSql);
        repo.VerifyAll();
    }


    [Fact]
    public async Task GetTablePageAsync_Throws_WhenTableDoesNotExist()
    {
        // Arrange
        var repo = new Mock<ISqliteRepository>(MockBehavior.Strict);
        repo.Setup(r => r.ObjectExistsAsync("table", "Users", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var svc = CreateService(repo);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.GetTablePageAsync("Users", page: 1, pageSize: 50, CancellationToken.None));

        repo.VerifyAll();
    }

    [Fact]
    public async Task GetTablePageAsync_WhenZeroRows_ReturnsEmptyPage_WithPaginationDefaults()
    {
        // Arrange
        var repo = new Mock<ISqliteRepository>(MockBehavior.Strict);

        repo.Setup(r => r.ObjectExistsAsync("table", "Users", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // COUNT(*) = 0
        repo.Setup(r => r.CountRowsAsync("\"Users\"", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0L);

        var svc = CreateService(repo);

        // Act
        var page = await svc.GetTablePageAsync("Users", page: 5, pageSize: 100, CancellationToken.None);

        // Assert
        // Paginator ensures TotalPages >= 1, and clamps page to [1..TotalPages]
        Assert.Equal("table", page.Type);
        Assert.Equal("Users", page.Name);
        Assert.Equal(1, page.Page);                // clamped
        Assert.Equal(100, page.PageSize);
        Assert.Equal(0, page.TotalRows);
        Assert.Equal(1, page.TotalPages);
        Assert.Empty(page.Data);

        repo.VerifyAll();
    }

    [Fact]
    public async Task GetTablePageAsync_UsesRowIdOrdering_WhenNotWithoutRowId()
    {
        // Arrange
        var repo = new Mock<ISqliteRepository>(MockBehavior.Strict);

        repo.Setup(r => r.ObjectExistsAsync("table", "Users", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        repo.Setup(r => r.CountRowsAsync("\"Users\"", It.IsAny<CancellationToken>()))
            .ReturnsAsync(123L);

        // IsWithoutRowId = false -> orderByRowId = true
        repo.Setup(r => r.IsWithoutRowIdAsync("Users", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        IReadOnlyList<Dictionary<string, object?>> rows =
        [
            new() { ["Id"] = 1, ["Name"] = "A" }
        ];

        // Capture parameters to validate orderByRowId=true
        bool? capturedOrderByRowId = null;
        int? capturedTake = null;
        int? capturedOffset = null;

        repo.Setup(r => r.GetPageAsync("\"Users\"", It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, bool, int, int, CancellationToken>((_, orderByRowId, take, offset, _) =>
            {
                capturedOrderByRowId = orderByRowId;
                capturedTake = take;
                capturedOffset = offset;
            })
            .ReturnsAsync(rows);

        var svc = CreateService(repo);

        // Act
        var page = await svc.GetTablePageAsync("Users", page: 2, pageSize: 50, CancellationToken.None);

        // Assert
        Assert.True(capturedOrderByRowId);
        Assert.Equal(50, capturedTake);
        Assert.Equal(50, capturedOffset); // (page-1)*size

        Assert.Equal("table", page.Type);
        Assert.Equal("Users", page.Name);
        Assert.Equal(2, page.Page);
        Assert.Equal(50, page.PageSize);
        Assert.Equal(123, page.TotalRows);
        Assert.Equal((int)Math.Ceiling(123 / 50d), page.TotalPages);
        Assert.Same(rows, page.Data);

        repo.VerifyAll();
    }

    [Fact]
    public async Task GetTablePageAsync_AvoidsRowIdOrdering_WhenWithoutRowId()
    {
        // Arrange
        var repo = new Mock<ISqliteRepository>(MockBehavior.Strict);

        repo.Setup(r => r.ObjectExistsAsync("table", "NoRowIdTable", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        repo.Setup(r => r.CountRowsAsync("\"NoRowIdTable\"", It.IsAny<CancellationToken>()))
            .ReturnsAsync(10L);

        // IsWithoutRowId = true -> orderByRowId = false
        repo.Setup(r => r.IsWithoutRowIdAsync("NoRowIdTable", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        IReadOnlyList<Dictionary<string, object?>> rows =
        [
            new() { ["k"] = 1 }
        ];

        bool? capturedOrderByRowId = null;

        repo.Setup(r => r.GetPageAsync("\"NoRowIdTable\"", It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, bool, int, int, CancellationToken>((_, orderByRowId, _, _, _) => capturedOrderByRowId = orderByRowId)
            .ReturnsAsync(rows);

        var svc = CreateService(repo);

        // Act
        var page = await svc.GetTablePageAsync("NoRowIdTable", page: 1, pageSize: 10, CancellationToken.None);

        // Assert
        Assert.False(capturedOrderByRowId);
        Assert.Equal("table", page.Type);
        Assert.Equal("NoRowIdTable", page.Name);
        Assert.Equal(10, page.TotalRows);
        Assert.Same(rows, page.Data);

        repo.VerifyAll();
    }

    // ---------- Paging: Views ----------

    [Fact]
    public async Task GetViewPageAsync_Throws_WhenViewDoesNotExist()
    {
        // Arrange
        var repo = new Mock<ISqliteRepository>(MockBehavior.Strict);
        repo.Setup(r => r.ObjectExistsAsync("view", "ActiveUsers", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var svc = CreateService(repo);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.GetViewPageAsync("ActiveUsers", page: 1, pageSize: 50, CancellationToken.None));

        repo.VerifyAll();
    }

    [Fact]
    public async Task GetViewPageAsync_WhenZeroRows_ReturnsEmptyPage()
    {
        // Arrange
        var repo = new Mock<ISqliteRepository>(MockBehavior.Strict);

        repo.Setup(r => r.ObjectExistsAsync("view", "ActiveUsers", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        repo.Setup(r => r.CountRowsAsync("\"ActiveUsers\"", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0L);

        var svc = CreateService(repo);

        // Act
        var page = await svc.GetViewPageAsync("ActiveUsers", page: 3, pageSize: 25, CancellationToken.None);

        // Assert
        Assert.Equal("view", page.Type);
        Assert.Equal("ActiveUsers", page.Name);
        Assert.Equal(1, page.Page);    // clamped because totalRows = 0 -> totalPages = 1
        Assert.Equal(25, page.PageSize);
        Assert.Equal(0, page.TotalRows);
        Assert.Equal(1, page.TotalPages);
        Assert.Empty(page.Data);

        repo.VerifyAll();
    }

    [Fact]
    public async Task GetViewPageAsync_NeverUsesRowIdOrdering()
    {
        // Arrange
        var repo = new Mock<ISqliteRepository>(MockBehavior.Strict);

        repo.Setup(r => r.ObjectExistsAsync("view", "ActiveUsers", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        repo.Setup(r => r.CountRowsAsync("\"ActiveUsers\"", It.IsAny<CancellationToken>()))
            .ReturnsAsync(42L);

        IReadOnlyList<Dictionary<string, object?>> rows =
        [
            new() { ["Id"] = 7, ["Name"] = "Z" }
        ];

        bool? capturedOrderByRowId = null;

        repo.Setup(r => r.GetPageAsync("\"ActiveUsers\"", It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, bool, int, int, CancellationToken>((_, orderByRowId, _, _, _) => capturedOrderByRowId = orderByRowId)
            .ReturnsAsync(rows);

        var svc = CreateService(repo);

        // Act
        var page = await svc.GetViewPageAsync("ActiveUsers", page: 2, pageSize: 20, CancellationToken.None);

        // Assert
        Assert.False(capturedOrderByRowId); // always false for views
        Assert.Equal("view", page.Type);
        Assert.Equal(42, page.TotalRows);
        Assert.Same(rows, page.Data);

        repo.VerifyAll();
    }
}
