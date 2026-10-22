using DirectoryManager.Web.Models.Reviews;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class ReviewListViewModelTests
    {
        [Theory]
        [InlineData(0, 50, 1, 1, 0, 0)]
        [InlineData(120, 50, 1, 3, 1, 50)]
        [InlineData(120, 50, 3, 3, 101, 120)]
        [InlineData(100, 50, 2, 2, 51, 100)]
        public void Paging_Math(int total, int size, int page, int pages, int first, int last)
        {
            var vm = new ReviewListViewModel { Total = total, PageSize = size, Page = page };

            Assert.Equal(pages, vm.TotalPages);
            Assert.Equal(first, vm.FirstItem);
            Assert.Equal(last, vm.LastItem);
        }
    }
}
