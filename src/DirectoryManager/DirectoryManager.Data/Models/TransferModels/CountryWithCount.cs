
namespace DirectoryManager.Data.Models.TransferModels
{
    public class CountryWithCount
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Key { get; set; } = ""; // slug (from name)
        public int Count { get; set; }
    }
}
