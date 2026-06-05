namespace DirectoryManager.Web.Models.Sponsorship
{
    public class SponsorEmailsViewModel
    {
        public bool PaidOnly { get; set; } = true;

        public IReadOnlyList<SponsorEmailRow> Rows { get; set; } = new List<SponsorEmailRow>();

        /// <summary>
        /// Distinct emails joined for easy copy/paste (newline separated).
        /// </summary>
        public string EmailsNewlineSeparated { get; set; } = string.Empty;

        /// <summary>
        /// Distinct emails joined for easy copy/paste (comma separated, e.g. for a BCC field).
        /// </summary>
        public string EmailsCommaSeparated { get; set; } = string.Empty;

        public int DistinctEmailCount => this.Rows.Count;
    }

}
