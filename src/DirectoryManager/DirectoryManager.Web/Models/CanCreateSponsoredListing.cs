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
                sb.Append(timeDifference.Days == 1 ? $"{timeDifference.Days} day " : $"{timeDifference.Days} days ");
                sb.Append(timeDifference.Hours == 1 ? $"{timeDifference.Hours} hour " : $"{timeDifference.Hours} hours ");
                sb.Append(timeDifference.Minutes == 1 ? $"{timeDifference.Minutes} minute " : $"{timeDifference.Minutes} minutes ");
                sb.Append(timeDifference.Seconds == 1 ? $"{timeDifference.Seconds} second " : $"{timeDifference.Seconds} seconds ");

                // Display the result as one string
                return sb.ToString();
            }
        }
    }
}
