using System.Net;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Net.Http.Headers;

namespace DirectoryManager.Web.AppRules
{
    public class RedirectWwwToNonWwwRule : IRule
    {
        public int StatusCode { get; } = (int)HttpStatusCode.MovedPermanently;
        public bool ExcludeLocalhost { get; set; } = true;

        public void ApplyRule(RewriteContext context)
        {
            var request = context.HttpContext.Request;
            var host = request.Host;
            if (!host.Host.StartsWith("www", StringComparison.OrdinalIgnoreCase))
            {
                context.Result = RuleResult.ContinueRules;
                return;
            }

            if (this.ExcludeLocalhost && string.Equals(host.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                context.Result = RuleResult.ContinueRules;
                return;
            }

            var newHost = !string.IsNullOrEmpty(host.Value) ? host.Value.Replace("www.", string.Empty) : string.Empty;

            var newLocation = string.Format(
                "{0}://{1}{2}{3}{4}",
                request.Scheme,
                newHost,
                request.PathBase,
                request.Path,
                request.QueryString);

            var response = context.HttpContext.Response;
            response.StatusCode = this.StatusCode;
            response.Headers[HeaderNames.Location] = newLocation;
            context.Result = RuleResult.EndResponse;
        }
    }
}