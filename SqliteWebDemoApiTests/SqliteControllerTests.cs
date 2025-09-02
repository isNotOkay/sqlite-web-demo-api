using Microsoft.AspNetCore.Mvc;
using Moq;
using SqliteWebDemoApi.Controllers;
using SqliteWebDemoApi.Models;
using SqliteWebDemoApi.Services;

namespace SqliteWebDemoApiTest
{
    public sealed class SqliteControllerTests
    {
        private static SqliteController CreateController(Mock<ISqliteService> browserMock)
            => new(browserMock.Object);

        [Fact]
        public async Task GetTables_ReturnsOk_WithItemsAndTotal()
        {
            // Arrange
            var items = new List<SqliteRelationInfo>
            {
                new() { Name = "Users", RowCount = 3, Columns = ["Id", "Name"] }
            };

            var browser = new Mock<ISqliteService>(MockBehavior.Strict);
            browser.Setup(b => b.ListTablesAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync((items, items.Count));

            var controller = CreateController(browser);

            // Act
            var result = await controller.GetTables(CancellationToken.None);

            // Assert (controller returns Ok(dto) -> Result is OkObjectResult; Value is null)
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<ListResponse<SqliteRelationInfo>>(ok.Value);

            Assert.Equal(items, payload.Items);
            Assert.Equal(items.Count, payload.Total);

            browser.VerifyAll();
        }

        [Fact]
        public async Task GetViews_ReturnsOk_WithItemsAndTotal()
        {
            // Arrange
            var items = new List<SqliteRelationInfo>
            {
                new() { Name = "ActiveUsers", RowCount = 10, Columns = ["Id"] }
            };

            var browser = new Mock<ISqliteService>(MockBehavior.Strict);
            browser.Setup(b => b.ListViewsAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync((items, items.Count));

            var controller = CreateController(browser);

            // Act
            var result = await controller.GetViews(CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<ListResponse<SqliteRelationInfo>>(ok.Value);

            Assert.Equal(items, payload.Items);
            Assert.Equal(items.Count, payload.Total);

            browser.VerifyAll();
        }

        [Fact]
        public async Task GetTableData_ReturnsOk_WithPagedResult()
        {
            var pageResult = new PagedResult<Dictionary<string, object?>>
            {
                Type = "table",
                Name = "Users",
                Page = 2,
                PageSize = 50,
                TotalRows = 123,
                TotalPages = 3,
                Data = new List<Dictionary<string, object?>>
                {
                    new() { ["Id"] = 1, ["Name"] = "Alice" }
                }
            };

            var browser = new Mock<ISqliteService>(MockBehavior.Strict);
            browser.Setup(b => b.GetTablePageAsync("Users", 2, 50, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(pageResult);

            var controller = CreateController(browser);

            var result = await controller.GetTableData("Users", page: 2, pageSize: 50, ct: CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Same(pageResult, ok.Value);
            browser.VerifyAll();
        }

        [Fact]
        public async Task GetTableData_ReturnsBadRequest_OnArgumentException()
        {
            var browser = new Mock<ISqliteService>(MockBehavior.Strict);
            browser.Setup(b => b.GetTablePageAsync("Bad Id", 1, 50, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new ArgumentException("Invalid identifier.", "tableId"));

            var controller = CreateController(browser);

            var result = await controller.GetTableData("Bad Id", page: 1, pageSize: 50, ct: CancellationToken.None);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Invalid identifier", bad.Value!.ToString());
            browser.VerifyAll();
        }

        [Fact]
        public async Task GetTableData_ReturnsNotFound_OnKeyNotFoundException()
        {
            var browser = new Mock<ISqliteService>(MockBehavior.Strict);
            browser.Setup(b => b.GetTablePageAsync("Missing", 1, 50, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new KeyNotFoundException("Table \"Missing\" not found."));

            var controller = CreateController(browser);

            var result = await controller.GetTableData("Missing", page: 1, pageSize: 50, ct: CancellationToken.None);

            var nf = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("not found", nf.Value!.ToString());
            browser.VerifyAll();
        }

        [Fact]
        public async Task GetTableData_UsesDefaultPaging_AndDefaultSorting_WhenNotProvided()
        {
            int? capturedPage = null;
            int? capturedPageSize = null;
            var capturedSortBy = "SENTINEL";
            var capturedSortDir = "SENTINEL";

            var pageResult = new PagedResult<Dictionary<string, object?>>
            {
                Type = "table",
                Name = "Users",
                Page = 1,
                PageSize = 50,
                TotalRows = 0,
                TotalPages = 1,
                Data = new List<Dictionary<string, object?>>()
            };

            var browser = new Mock<ISqliteService>(MockBehavior.Strict);
            browser.Setup(b => b.GetTablePageAsync(
                            "Users",
                            It.IsAny<int>(),
                            It.IsAny<int>(),
                            It.IsAny<string?>(),
                            It.IsAny<string?>(),
                            It.IsAny<CancellationToken>()))
                   .Callback<string, int, int, string?, string?, CancellationToken>((_, p, s, sb, sd, _) =>
                   {
                       capturedPage = p;
                       capturedPageSize = s;
                       capturedSortBy = sb;
                       capturedSortDir = sd;
                   })
                   .ReturnsAsync(pageResult);

            var controller = CreateController(browser);

            // Call without page/pageSize/sortBy/sortDir: controller defaults kick in (1, 50, null, "asc")
            var result = await controller.GetTableData("Users", ct: CancellationToken.None);

            Assert.Equal(1, capturedPage);
            Assert.Equal(50, capturedPageSize);
            Assert.Null(capturedSortBy);
            Assert.Equal("asc", capturedSortDir);
            Assert.IsType<OkObjectResult>(result);
            browser.VerifyAll();
        }

        [Fact]
        public async Task GetViewData_ReturnsOk_WithPagedResult()
        {
            var pageResult = new PagedResult<Dictionary<string, object?>>
            {
                Type = "view",
                Name = "ActiveUsers",
                Page = 1,
                PageSize = 25,
                TotalRows = 42,
                TotalPages = 2,
                Data = new List<Dictionary<string, object?>>
                {
                    new() { ["Id"] = 7, ["Name"] = "Z" }
                }
            };

            var browser = new Mock<ISqliteService>(MockBehavior.Strict);
            browser.Setup(b => b.GetViewPageAsync("ActiveUsers", 1, 25, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(pageResult);

            var controller = CreateController(browser);

            var result = await controller.GetViewData("ActiveUsers", page: 1, pageSize: 25, ct: CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Same(pageResult, ok.Value);
            browser.VerifyAll();
        }

        [Fact]
        public async Task GetViewData_ErrorMapping_Works()
        {
            var browser = new Mock<ISqliteService>(MockBehavior.Strict);

            browser.Setup(b => b.GetViewPageAsync("bad", 1, 50, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new ArgumentException("Invalid identifier.", "viewId"));

            var controller = CreateController(browser);
            var bad = await controller.GetViewData("bad", page: 1, pageSize: 50, ct: CancellationToken.None);
            Assert.IsType<BadRequestObjectResult>(bad);

            browser.Reset();

            browser.Setup(b => b.GetViewPageAsync("missing", 1, 50, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new KeyNotFoundException("View \"missing\" not found."));

            controller = CreateController(browser);
            var nf = await controller.GetViewData("missing", page: 1, pageSize: 50, ct: CancellationToken.None);
            Assert.IsType<NotFoundObjectResult>(nf);
        }
    }
}
