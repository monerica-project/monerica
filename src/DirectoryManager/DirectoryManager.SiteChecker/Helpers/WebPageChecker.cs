namespace DirectoryManager.SiteChecker.Helpers
{
    public class WebPageChecker
    {
        private readonly string userAgentHeader;

        public WebPageChecker(string userAgentHeader)
        {
            this.userAgentHeader = userAgentHeader;
        }

        public async Task<bool> IsWebPageOnlineAsync(Uri uri)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(this.userAgentHeader);

            try
            {
                var response = await client.GetAsync(uri);

                if (response.IsSuccessStatusCode || (int)response.StatusCode > 400)
                {
                    return true;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Moved ||
                         response.RequestMessage?.RequestUri != uri)
                {
                    return false;
                }
                return false;
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
        }

        public bool IsWebPageOnlinePing(Uri uri)
        {
            try
            {
                var ping = new System.Net.NetworkInformation.Ping();
                var result = ping.Send(uri.Host);
                return result.Status == System.Net.NetworkInformation.IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }
    }
}
