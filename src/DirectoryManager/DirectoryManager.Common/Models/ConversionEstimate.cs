namespace DirectoryManager.Common.Models
{
    public class ConversionEstimate
    {
        public decimal EstimatedAmount { get; init; }
        public string FromCurrency { get; init; } = string.Empty;
        public string ToCurrency { get; init; } = string.Empty;
    }
}