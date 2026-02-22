namespace DirectoryManager.Web.Models.Sponsorship
{

    public class SponsorshipIndexVm
    {
        // search
        public string Query { get; set; } = "";
        public bool HasSearched { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public List<SponsorshipSearchItemVm> Results { get; set; } = new ();
        public WaitlistBoardVm WaitlistBoard { get; set; } = new ();
        public RecentPaidVm RecentPaid { get; set; } = new ();
    }
}
