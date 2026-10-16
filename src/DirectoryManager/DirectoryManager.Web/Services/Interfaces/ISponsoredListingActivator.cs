using DirectoryManager.Data.Models.SponsoredListings;

namespace DirectoryManager.Web.Services.Interfaces
{
    // Saves a sponsorship invoice and, once it is paid, makes sure the sponsored listing it paid
    // for exists (a new listing, or an extension of the buyer's current one). Shared by the
    // payment webhooks and the admin invoice edit so a paid invoice can always be activated.
    public interface ISponsoredListingActivator
    {
        // Persists the invoice, then activates it when paid. Idempotent: running it again for an
        // invoice that is already linked to its listing does nothing. Throws when a save fails,
        // so a webhook answers non-2xx and the payment processor retries.
        Task SaveAndActivateAsync(SponsoredListingInvoice invoice);

        // True when the invoice is paid but no sponsored listing has been linked to it yet.
        bool NeedsActivation(SponsoredListingInvoice invoice);
    }
}
