using System.Net;
using System.Net.NetworkInformation;

namespace DirectoryManager.SiteChecker.Helpers
{
    public class WebPageChecker
    {
        private readonly HttpClient http;

        public WebPageChecker(string userAgent, TimeSpan? timeout = null)
        {
            this.http = new HttpClient
            {
                Timeout = timeout ?? TimeSpan.FromSeconds(60)
            };
            this.http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        }

        public async Task<bool> IsOnlineAsync(Uri uri)
        {
            // 1) Try HEAD -> if it gives success (2xx) or redirect (3xx), we know it's online.
            try
            {
                using var headReq = new HttpRequestMessage(HttpMethod.Head, uri);
                using var headResp = await this.http
                    .SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);

                if ((int)headResp.StatusCode is >= 200 and < 400)
                {
                    return true;
                }

                var webServerDownStatusCode = 521;
                if ((int)headResp.StatusCode == webServerDownStatusCode)
                {
                    return false;
                }

                // otherwise (including 404, 4xx, 5xx) fall through to GET
            }
            catch (HttpRequestException hre) when (hre.StatusCode == HttpStatusCode.MethodNotAllowed)
            {
                // HEAD not allowed → fall back to GET
            }
            catch
            {
                // HEAD failed → fall back to GET
            }

            // 2) Try GET -> if 404, offline; any other status (2xx, 3xx, 4xx≠404, 5xx) = online
            try
            {
                using var getReq = new HttpRequestMessage(HttpMethod.Get, uri);
                using var getResp = await this.http
                    .SendAsync(getReq, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);

                if (getResp.StatusCode == HttpStatusCode.NotFound)
                {
                    return false;
                }

                return true;
            }
            catch
            {
                // GET failed → fall back to ping
            }

            // 3) Fallback to ICMP ping
            try
            {
                using var ping = new Ping();
                var result = await ping
                    .SendPingAsync(uri.Host, TimeSpan.FromSeconds(1))
                    .ConfigureAwait(false);
                return result.Status == IPStatus.Success;
            }
            catch
            {
                // ping failed
            }

            // Everything failed → treat as offline
            return false;
        }
    }
}
