namespace DirectoryManager.Data.Models.TransferModels
{
    public sealed class CountryCountRow
    {
        public string CountryCode { get; set; } = string.Empty; // ISO2
        public int Count { get; set; }
    }
}