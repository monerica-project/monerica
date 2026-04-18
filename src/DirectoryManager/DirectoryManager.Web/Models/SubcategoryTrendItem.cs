namespace DirectoryManager.Web.Models
{
    public class SubcategoryTrendItem
    {
        public int? SubCategoryId { get; set; }
        public string SubCategoryName { get; set; } = "(Unknown)";
        public int StartCount { get; set; }
        public int EndCount { get; set; }
        public int Delta => this.EndCount - this.StartCount;

        // % change from start to end
        public double PercentChange
            => this.StartCount == 0
                ? (this.EndCount > 0 ? 100.0 : 0.0)
                : (this.EndCount - this.StartCount) * 100.0 / this.StartCount;
    }
}
