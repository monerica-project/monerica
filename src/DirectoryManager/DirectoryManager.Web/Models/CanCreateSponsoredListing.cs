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
                DateTime currentDate = DateTime.UtcNow;
                TimeSpan timeDifference = this.NextListingExpiration - currentDate;

                var sb = new StringBuilder();

                if (timeDifference.TotalHours < 1)
                {
                    sb.Append(timeDifference.Minutes == 1 ? $"{timeDifference.Minutes} minute " : $"{timeDifference.Minutes} minutes ");
                    sb.Append(timeDifference.Seconds == 1 ? $"{timeDifference.Seconds} second " : $"{timeDifference.Seconds} seconds ");
                }
                else
                {
                    sb.Append(timeDifference.Days == 1 ? $"{timeDifference.Days} day " : $"{timeDifference.Days} days ");
                    sb.Append(timeDifference.Hours == 1 ? $"{timeDifference.Hours} hour " : $"{timeDifference.Hours} hours ");
                    sb.Append(timeDifference.Minutes == 1 ? $"{timeDifference.Minutes} minute " : $"{timeDifference.Minutes} minutes ");
                }

                return sb.ToString();
            }
        }
    }
}