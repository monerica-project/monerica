namespace DirectoryManager.Console.Helpers
{

    public class WebPageChecker
    {
        public static async Task<bool> IsWebPageOnlineAsync(Uri uri)
        {
            using HttpClient client = new();
            try
            {
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
