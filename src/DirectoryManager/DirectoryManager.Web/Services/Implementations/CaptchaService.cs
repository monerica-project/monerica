using DirectoryManager.Web.Models;
using Microsoft.Extensions.Options;

namespace DirectoryManager.Web.Services.Interfaces
{
    public class CaptchaService : ICaptchaService
    {
        private readonly HttpClient http;
        private readonly CaptchaOptions options;
        private readonly IWebHostEnvironment env;

        public CaptchaService(IHttpClientFactory http, IOptions<CaptchaOptions> opts, IWebHostEnvironment env)
        {
            this.http = http.CreateClient();
            this.options = opts.Value;
            this.env = env;
        }

        public bool IsValid(HttpRequest request)
        {
            // posted captcha
            var posted = (request.Form["Captcha"].ToString() ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(posted))
            {
                return false;
            }

            // context from form (preferred) or query (fallback) — mirrors your markup
            var ctx = (request.Form["CaptchaContext"].ToString() ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(ctx))
            {
                ctx = (request.Query["ctx"].ToString() ?? string.Empty).Trim();
            }

            if (string.IsNullOrEmpty(ctx))
            {
                ctx = "default";
            }

            var keyCtx = $"CaptchaCode:{ctx}";
            var sess = request.HttpContext.Session;

            // read
            var expected = sess.GetString(keyCtx) ?? sess.GetString("CaptchaCode");

            // consume (one-time)
            sess.Remove(keyCtx);
            sess.Remove("CaptchaCode");

            // compare, case-insensitive
            return !string.IsNullOrEmpty(expected) &&
                   string.Equals(expected, posted, StringComparison.OrdinalIgnoreCase);
        }
    }
}