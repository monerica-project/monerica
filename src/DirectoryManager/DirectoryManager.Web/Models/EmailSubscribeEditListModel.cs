namespace DirectoryManager.Web.Models
{
    public class EmailSubscribeEditListModel
    {
        public List<EmailSubscribeEditModel> Items { get; set; } = new List<EmailSubscribeEditModel>();

        public string Emails { get; set; } = default!;

        public string UnsubscribeLink { get; set; } = default!;

        public int TotalSubscribed { get; set; }
    }
}
