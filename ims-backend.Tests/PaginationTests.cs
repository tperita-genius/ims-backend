using Xunit;

namespace ims_backend.Tests;

public class PaginationTests
{
    [Theory]
    [InlineData(0, 10, 1)]   // 0 筆資料時顯示 1 頁
    [InlineData(10, 10, 1)]  // 剛好 10 筆顯示 1 頁
    [InlineData(11, 10, 2)]  // 11 筆顯示 2 頁
    [InlineData(95, 10, 10)] // 95 筆顯示 10 頁
    public void TotalPages_Calculation_Should_Be_Correct(int totalCount, int pageSize, int expectedPages)
    {
        // Act
        int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling((double)totalCount / pageSize);

        // Assert
        Assert.Equal(expectedPages, totalPages);
    }
}