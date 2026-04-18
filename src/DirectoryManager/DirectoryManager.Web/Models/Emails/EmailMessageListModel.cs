using DirectoryManager.Web.Models.Emails;

namespace DirectoryManager.Web.Models
{
    public class EmailMessageListModel
    {
        public List<EmailMessageModel> Messages { get; set; } = new List<EmailMessageModel>();
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }
}