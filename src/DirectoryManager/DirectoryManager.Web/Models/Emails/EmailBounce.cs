namespace DirectoryManager.Web.Models.Emails
{
    public class EmailBounce
    {
        public EmailBounce(string status, string reason, string email, long createdTimestamp)
        {
            this.Status = status;
            this.Reason = reason;
            this.Email = email;
            this.Created = DateTimeOffset.FromUnixTimeSeconds(createdTimestamp).UtcDateTime;
        }

        // Parameterless constructor for serialization/deserialization frameworks like CsvHelper
        public EmailBounce()
        {
        }

        public string Status { get; set; }
        public string Reason { get; set; }
        public string Email { get; set; }
        public DateTime Created { get; set; }
    }
}