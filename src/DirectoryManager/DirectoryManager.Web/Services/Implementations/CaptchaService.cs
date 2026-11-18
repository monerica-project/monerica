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

            // Same normalisation as where the image stores its code, so the keys always line up.
            var keyCtx = DirectoryManager.Web.Helpers.CaptchaTools.SessionKey(
                DirectoryManager.Web.Helpers.CaptchaTools.NormalizeContext(ctx));
            var sess = request.HttpContext.Session;

            // read
            var expected = sess.GetString(keyCtx) ?? sess.GetString(DirectoryManager.Web.Helpers.CaptchaTools.LegacySessionKey);

            // consume (one-time). One image stores its code under its own context AND under the
            // legacy key, so every copy of this code is removed: otherwise one solved image could
            // be used twice, once through each key.
            var copies = sess.Keys
                .Where(k => k.StartsWith(DirectoryManager.Web.Helpers.CaptchaTools.LegacySessionKey, StringComparison.Ordinal))
                .Where(k => k == keyCtx
                            || k == DirectoryManager.Web.Helpers.CaptchaTools.LegacySessionKey
                            || (!string.IsNullOrEmpty(expected) && string.Equals(sess.GetString(k), expected, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            foreach (var k in copies)
            {
                sess.Remove(k);
            }

            // compare, case-insensitive
            return !string.IsNullOrEmpty(expected) &&
                   string.Equals(expected, posted, StringComparison.OrdinalIgnoreCase);
        }
    }
}