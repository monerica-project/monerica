using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace DirectoryManager.Web.Models.Emails
{
    public class EmailBounceMap : ClassMap<EmailBounce>
    {
        public EmailBounceMap()
        {
            this.Map(m => m.Status).Name("status");
            this.Map(m => m.Reason).Name("reason");
            this.Map(m => m.Email).Name("email");
            this.Map(m => m.Created).Convert(args =>
            {
                var timestamp = args.Row.GetField<long>("created");
                return DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
            });
        }

        public static IEnumerable<EmailBounce> ReadBouncesFromCsv(string filePath)
        {
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null, // Ignore missing headers or case mismatch
                MissingFieldFound = null, // Ignore missing fields
                PrepareHeaderForMatch = args => args.Header.ToLower(), // Normalize headers to lowercase
            });

            csv.Context.RegisterClassMap<EmailBounceMap>();
            return csv.GetRecords<EmailBounce>().ToList();
        }
    }
}