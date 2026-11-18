namespace DirectoryManager.GuaranteeSync
{
    public sealed class GuaranteeSyncOptions
    {
        public string ApiUrl { get; set; } = "https://orangefren.com/api/monerica/guarantees";

        // The link stored on a listing's guarantee row: OrangeFren's public guarantee page, the
        // page a visitor is sent to as proof. The API's own "goto" links are redirects to the
        // exchange, not proof pages, so they are not used.
        public string ProofLink { get; set; } = "https://orangefren.com/guarantee";

        // A response with fewer services than this is treated as broken and ignored.
        public int MinServices { get; set; } = 5;

        // OrangeFren service_name -> Monerica listing key, for names that do not match by themselves.
        public Dictionary<string, string> Aliases { get; set; } = new (StringComparer.OrdinalIgnoreCase);
    }
}
