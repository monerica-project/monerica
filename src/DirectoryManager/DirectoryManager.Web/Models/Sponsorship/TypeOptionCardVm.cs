using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.Sponsorship
{
    public class TypeOptionCardVm
    {
        // The option panel you already build in the controller
        public SponsorshipTypeOptionVm Option { get; set; } = new ();

        // Context needed to build checkout links
        public int DirectoryEntryId { get; set; }
        public int? TypeId { get; set; } // null for MainSponsor, categoryId for category, subcategoryId for subcat

        public bool CanAdvertise { get; set; }

        // You can pass this in, but the partial can also compute Url.Action
        public string CheckoutUrl { get; set; } = "/sponsoredlisting/start";

        // HTML from snippets/settings
        public string DetailsHtml { get; set; } = "";

        // Where "extend active" lives
        public string ExtendUrl { get; set; } = "/sponsoredlisting/activelistings";
    }
}
