
using System.Text.Json;
using DirectoryManager.Web.Enums;
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

        private async Task<bool> IsValidAsync(HttpRequest request)
        {
            // Local dev convenience
            if (this.options.FailOpenForLocalhost &&
                this.env.IsDevelopment() &&
                (request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                 request.Host.Host.StartsWith("127.") ||
                 request.Host.Host.Equals("::1")))
            {
                return true;
            }

            // Get token based on provider (but also accept any known field)
            string? token = null;
            switch (this.options.Provider)
            {
                case CaptchaProvider.Turnstile:
                    token = request.Form["cf-turnstile-response"].FirstOrDefault();
                    break;
                case CaptchaProvider.HCaptcha:
                    token = request.Form["h-captcha-response"].FirstOrDefault();
                    break;
                case CaptchaProvider.ReCaptcha:
                    token = request.Form["g-recaptcha-response"].FirstOrDefault();
                    break;
            }

            // Fallback: try any known token name if specific one missing
            token ??= request.Form["cf-turnstile-response"].FirstOrDefault()
                   ?? request.Form["h-captcha-response"].FirstOrDefault()
                   ?? request.Form["g-recaptcha-response"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(token))
                return false;

            var ip = request.HttpContext.Connection.RemoteIpAddress?.ToString();

            return this.options.Provider switch
            {
                CaptchaProvider.Turnstile => await this.VerifyTurnstileAsync(token, ip),
                CaptchaProvider.HCaptcha => await this.VerifyHCaptchaAsync(token, ip),
                CaptchaProvider.ReCaptcha => await this.VerifyReCaptchaAsync(token, ip),
                _ => false
            };
        }

        private async Task<bool> VerifyTurnstileAsync(string token, string? ip)
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["secret"] = this.options.SecretKey,
                ["response"] = token,
                ["remoteip"] = ip
            }!);

            using var res = await this.http.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", content);
            if (!res.IsSuccessStatusCode) return false;

            var json = await res.Content.ReadAsStringAsync();
            var result = System.Text.Json.JsonSerializer.Deserialize<VerifyResult>(json);
            return result?.Success == true;
        }

        private async Task<bool> VerifyHCaptchaAsync(string token, string? ip)
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["secret"] = this.options.SecretKey,
                ["response"] = token,
                ["remoteip"] = ip
            }!);

            using var res = await this.http.PostAsync("https://hcaptcha.com/siteverify", content);
            if (!res.IsSuccessStatusCode) return false;

            var json = await res.Content.ReadAsStringAsync();
            var result = System.Text.Json.JsonSerializer.Deserialize<VerifyResult>(json);
            return result?.Success == true;
        }

        private async Task<bool> VerifyReCaptchaAsync(string token, string? ip)
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["secret"] = this.options.SecretKey,
                ["response"] = token,
                ["remoteip"] = ip
            }!);

            using var res = await this.http.PostAsync("https://www.google.com/recaptcha/api/siteverify", content);
            if (!res.IsSuccessStatusCode) return false;

            var json = await res.Content.ReadAsStringAsync();
            var result = System.Text.Json.JsonSerializer.Deserialize<VerifyResult>(json);
            return result?.Success == true;
        }
    }

}
