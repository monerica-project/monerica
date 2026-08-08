namespace DirectoryManager.Web.Models.Reports
{
    /// <summary>One piece of pricing/retention guidance for the income forecast.</summary>
    public class ForecastAdvice
    {
        /// <summary>"positive" (opportunity), "warning" (caution), or "info".</summary>
        public string Kind { get; set; } = "info";

        public string Text { get; set; } = string.Empty;
    }
}
