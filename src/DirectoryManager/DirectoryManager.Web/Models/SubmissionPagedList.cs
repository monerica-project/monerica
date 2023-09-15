using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Models
{
    public class SubmissionPagedList
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public List<Submission> Items { get; set; }
    }

}
