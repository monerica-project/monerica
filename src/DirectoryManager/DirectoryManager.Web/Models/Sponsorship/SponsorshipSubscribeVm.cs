namespace DirectoryManager.Web.Models.Sponsorship
{
    public class SponsorshipSubscribeVm
    {
        public int DirectoryEntryId { get; set; }
        public string Email { get; set; } = "";
        public bool NotifyMain { get; set; }
        public bool NotifyCategory { get; set; }
        public bool NotifySubcategory { get; set; }
    }
}
