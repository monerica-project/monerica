namespace DirectoryManager.Web.Models.Sponsorship
{
    public class SponsorshipOfferVm
    {
        public int Days { get; set; }
        public decimal PriceUsd { get; set; }
        public decimal PricePerDay { get; set; }
        public string Description { get; set; } = "";
    }
}
