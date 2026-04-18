namespace DirectoryManager.Web.Models.Emails
{
    public class PagedEmailSubscribeEditListModel
    {
        public List<EmailSubscribeEditModel> Items { get; set; } = new List<EmailSubscribeEditModel>();
        public int TotalSubscribed { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public bool HasPreviousPage => this.CurrentPage > 1;
        public bool HasNextPage => this.CurrentPage * this.PageSize < this.TotalItems;
    }
}