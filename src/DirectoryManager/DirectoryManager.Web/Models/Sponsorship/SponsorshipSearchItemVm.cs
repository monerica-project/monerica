namespace DirectoryManager.Web.Models.Sponsorship
{
    public class SponsorshipSearchItemVm
    {
        public int DirectoryEntryId { get; set; }
        public string Name { get; set; } = "";
        public string Link { get; set; } = "";
        public string DirectoryEntryKey { get; set; } = "";
        public string Status { get; set; } = "";
        public int AgeDays { get; set; }
        public string Category { get; set; } = "";
        public string Subcategory { get; set; } = "";

        public bool CanAdvertise { get; set; }
        public List<string> Reasons { get; set; } = new ();
    }
}
