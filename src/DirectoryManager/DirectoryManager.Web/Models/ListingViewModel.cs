namespace DirectoryManager.Web.Models
{
    public class ListingViewModel
    {
        public int Id { get; set; }
        required public string DirectoryEntryName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public bool IsActive
        {
            get
            {
                DateTime currentDate = DateTime.UtcNow;
                return this.StartDate <= currentDate && this.EndDate >= currentDate;
            }
        }
    }
}