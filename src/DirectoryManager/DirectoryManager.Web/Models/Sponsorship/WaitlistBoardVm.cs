namespace DirectoryManager.Web.Models.Sponsorship
{
    public class WaitlistBoardVm
    {
        public int MainWaitlistCount { get; set; }

        // Preview rows shown on /sponsorship
        public List<WaitlistPreviewRowVm> MainPreview { get; set; } = new ();

        public string? BrowseWaitlistUrl { get; set; }
    }
}
