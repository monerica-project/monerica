using System.Text;

namespace DirectoryManager.Web.Models
{
    public class ListingInventoryModel
    {
        public bool CanCreateSponsoredListing { get; set; }

        public DateTime NextListingExpiration { get; set; }

        public string TimeUntilNextMessage
        {
            get
            {
                // Get the current date and time
                DateTime currentDate = DateTime.UtcNow;

                // Calculate the difference
                TimeSpan timeDifference = this.NextListingExpiration - currentDate;

                // Display the difference in days, hours, minutes, and seconds
                // Use StringBuilder to concatenate the string
                var sb = new StringBuilder();
                sb.AppendLine($"{timeDifference.Days} days");
                sb.AppendLine($"{timeDifference.Hours} hours");
                sb.AppendLine($"{timeDifference.Minutes} minutes");
                sb.AppendLine($"{timeDifference.Seconds} seconds");

                // Display the result as one string
                return sb.ToString();
            }
        }
    }
}
