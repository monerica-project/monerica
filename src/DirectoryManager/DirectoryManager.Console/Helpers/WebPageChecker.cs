namespace DirectoryManager.Console.Helpers
{

    public class WebPageChecker
    {
        const string DefaultHeader = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36";

        public static async Task<bool> IsWebPageOnlineAsync(Uri uri)
        {
            using HttpClient client = new();
            try
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultHeader);
                HttpResponseMessage response = await client.GetAsync(uri);

                var responseCode = response.StatusCode;

                if (responseCode == System.Net.HttpStatusCode.Moved ||
                    response?.RequestMessage?.RequestUri != uri)
                {
                    return false;
                }

                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException)
            {
                return false;
            }
        }
    }

}
