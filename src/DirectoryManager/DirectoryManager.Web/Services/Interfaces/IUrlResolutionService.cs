namespace DirectoryManager.Web.Services.Interfaces
{
    // IUrlResolutionService.cs
    public interface IUrlResolutionService
    {
        /// <summary>
        /// Is the incoming request on the Tor (.onion) host?
        /// </summary>
        bool IsTor { get; }

        /// <summary>
        /// The base URL to use for link generation:
        /// – empty string on Tor
        /// – otherwise the canonical domain (no trailing slash)
        /// </summary>
        string BaseUrl { get; }

        string ResolveToApp(string relativeOrAbsolutePath);

        string ResolveToRoot(string relativeOrAbsolutePath);
    }
}