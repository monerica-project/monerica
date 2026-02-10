using DirectoryManager.Web.Controllers;

namespace DirectoryManager.Web.Models.Sponsorship
{
    public class WaitlistPanelVm
    {
        public string ScopeLabel { get; set; } = "";
        public int Count { get; set; }
        public int JoinWouldBeRank { get; set; }
        public List<WaitlistPublicRowVm> Preview { get; set; } = new ();
        public string? BrowseUrl { get; set; }
        public string? Note { get; set; }

        public static WaitlistPanelVm Empty(string note) => new () { Note = note, Count = 0, JoinWouldBeRank = 1 };
    }
}
