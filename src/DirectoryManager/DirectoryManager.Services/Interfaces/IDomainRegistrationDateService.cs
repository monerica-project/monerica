namespace DirectoryManager.Services.Interfaces
{
    public interface IDomainRegistrationDateService
    {
        /// <summary>
        /// Returns the domain registration (creation) date for the URL's registrable domain.
        /// Returns null for onion/i2p links, IP hosts, invalid URLs, or if the date cannot be determined.
        /// </summary>
        Task<DateOnly?> GetDomainRegistrationDateAsync(string url, CancellationToken ct = default);
    }
}
