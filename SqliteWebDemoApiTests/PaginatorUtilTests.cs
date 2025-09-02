using SqliteWebDemoApi.Utilities;

namespace SqliteWebDemoApiTest;

public sealed class PaginatorUtilTests
{
    [Theory]
    [InlineData(1, 10, 95,  1, 10, 10,  0)]  // first page
    [InlineData(2, 10, 95,  2, 10, 10, 10)]  // second page
    [InlineData(10,10, 95, 10, 10, 10, 90)]  // last page
    [InlineData(11,10, 95, 10, 10, 10, 90)]  // too high, clamped to last
    public void Paginate_NormalCases(
        int requestedPage, int requestedPageSize, long totalRows,
        int expectedPage, int expectedPageSize, int expectedTotalPages, int expectedOffset)
    {
        var (page, pageSize, totalPages, offset) = PaginatorUtil.Paginate(requestedPage, requestedPageSize, totalRows);

        Assert.Equal(expectedPage, page);
        Assert.Equal(expectedPageSize, pageSize);
        Assert.Equal(expectedTotalPages, totalPages);
        Assert.Equal(expectedOffset, offset);
    }


    [Fact]
    public void Paginate_PageSizeIsClampedToMax()
    {
        var (page, pageSize, totalPages, offset) = PaginatorUtil.Paginate(1, 5000, totalRows: 100);

        Assert.Equal(1, page);
        Assert.Equal(1000, pageSize); // clamped
        Assert.Equal(1, totalPages);  // 100/1000 => 1 page
        Assert.Equal(0, offset);
    }

    [Fact]
    public void Paginate_PageSizeBelowOneIsClampedUp()
    {
        var (page, pageSize, totalPages, offset) = PaginatorUtil.Paginate(1, 0, totalRows: 10);

        Assert.Equal(1, page);
        Assert.Equal(1, pageSize); // clamped
        Assert.Equal(10, totalPages);
        Assert.Equal(0, offset);
    }

    [Fact]
    public void Paginate_TotalRowsZero_StillAtLeastOnePage()
    {
        var (page, pageSize, totalPages, offset) = PaginatorUtil.Paginate(1, 10, totalRows: 0);

        Assert.Equal(1, page);
        Assert.Equal(10, pageSize);
        Assert.Equal(1, totalPages); // enforced minimum
        Assert.Equal(0, offset);
    }

    [Fact]
    public void Paginate_PageRequestedBelowOne_IsClampedUp()
    {
        var (page, pageSize, totalPages, offset) = PaginatorUtil.Paginate(0, 10, totalRows: 100);

        Assert.Equal(1, page);
        Assert.Equal(10, pageSize);
        Assert.Equal(10, totalPages);
        Assert.Equal(0, offset);
    }
}
