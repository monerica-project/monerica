using System.Net;
using System.Text.RegularExpressions;

namespace DirectoryManager.Utilities.Validation
{
    /// <summary>
    /// Refuses the content automated security probes and test scripts leave behind: text that
    /// calls itself a test or a probe, template-injection strings, placeholder names, reserved
    /// "example" domains, and links aimed at raw IP addresses or odd ports. Added after the
    /// 2026-11-16 scan, where about 400 such drafts and a handful of reviews were posted.
    /// Every rule returns a short visitor-facing reason, or null when the value is fine.
    /// </summary>
    public static class ProbeContentRules
    {
        public const string TestTextMessage =
            "This looks like test or placeholder text. Please write about the real service.";

        public const string TestNameMessage =
            "This looks like a test or placeholder name. Please enter the real name of the service.";

        public const string ReservedEmailMessage =
            "Please enter a real email address (example and test domains are not accepted).";

        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

        // Words security testers use for what they are doing. None belongs in a listing or a review.
        private static readonly Regex TesterWords = new (
            @"(?<![a-z0-9])(probe|probes|probing|ssrf|idor|xss|sqli|csrf|pentest|pen\s*test|penetration\s+test|sql\s+injection|captcha\s+bypass|bypass\s+test|hijack\s+proof|proof\s+of\s+concept|padding\s+text|lorem\s+ipsum|canary|security\s+(?:test|tests|testing|assessment|scan)|authori[sz]ed\s+(?:security\s+)?(?:test|testing|assessment|scan)|test\s+service|validation\s+purposes)(?![a-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexTimeout);

        // "test listing", "qa review", "audit submission" ... ("test order" is left out on purpose:
        // real reviewers do write about a small test order.)
        private static readonly Regex TestOfThing = new (
            @"(?<![a-z0-9])(test|testing|qa|audit|dummy|fake|sample)[\s_-]+(listing|review|submission|entry|edit|reply|comment)(?![a-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexTimeout);

        // Run tags such as "QA994", "audit-472", "test_17".
        private static readonly Regex RunTag = new (
            @"(?<![a-z0-9])(qa|audit|probe|idor|ssrf|test)[\s_-]?\d{2,}(?![a-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexTimeout);

        // Template-injection test strings: {{7*7}}, ${7*7}, #{7*7}, <%= 7*7 %>, or a bare 7*7.
        private static readonly Regex TemplateInjection = new (
            @"\{\{.*?\}\}|[$#]\{[^}]*\}|<%|%>|(?<!\d)\d{1,3}\s*\*\s*\d{1,3}(?!\d)",
            RegexOptions.CultureInvariant | RegexOptions.Singleline,
            RegexTimeout);

        private static readonly Regex PlaceholderName = new (
            @"^\s*(test|testing|tester|probe|asdf+|qwerty|foo|bar|zz+.*|x{3,}|\W*)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexTimeout);

        /// <summary>True when free text (description, note, review, reply) reads as a test or probe.</summary>
        /// <returns>True for test or probe text.</returns>
        public static bool LooksLikeTestText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            try
            {
                return TesterWords.IsMatch(text)
                    || TestOfThing.IsMatch(text)
                    || RunTag.IsMatch(text)
                    || TemplateInjection.IsMatch(text);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        /// <summary>True when a listing name is a placeholder ("test", "ZZProbe", "asdf") or test text.</summary>
        /// <returns>True for a placeholder name.</returns>
        public static bool LooksLikeTestName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                return PlaceholderName.IsMatch(name) || LooksLikeTestText(name);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        /// <summary>True for hosts reserved for documentation and testing (RFC 2606 / 6761).</summary>
        /// <returns>True for a reserved host.</returns>
        public static bool IsReservedHost(string? host)
        {
            var h = (host ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();
            if (h.Length == 0)
            {
                return false;
            }

            if (h == "localhost" || h == "example.com" || h == "example.org" || h == "example.net")
            {
                return true;
            }

            return h.EndsWith(".example.com", StringComparison.Ordinal)
                || h.EndsWith(".example.org", StringComparison.Ordinal)
                || h.EndsWith(".example.net", StringComparison.Ordinal)
                || h.EndsWith(".example", StringComparison.Ordinal)
                || h.EndsWith(".invalid", StringComparison.Ordinal)
                || h.EndsWith(".test", StringComparison.Ordinal)
                || h.EndsWith(".localhost", StringComparison.Ordinal)
                || h.EndsWith(".local", StringComparison.Ordinal);
        }

        // Hosts that exist for testing rather than for a real business: request catchers and echo
        // services, tunnels, wildcard DNS that spells out an IP address, and DNS-rebinding services
        // (a name that answers with a public address first and 127.0.0.1 next). All were used
        // against the submit form on 2026-11-16/17.
        private static readonly string[] TestingZones =
        [
            "nip.io", "sslip.io", "xip.io", "localtest.me", "lvh.me", "vcap.me", "lacolhost.com", "traefik.me",
            "1u.ms", "rbndr.us", "rebind.it", "rebind.network", "rebinder.net", "dnsrebind.it",
            "webhook.site", "beeceptor.com", "requestcatcher.com", "pipedream.net", "requestbin.com", "requestbin.net",
            "requestrepo.com", "requestinspector.com", "webhook.cool", "hookbin.com", "ptsv2.com", "ptsv3.com",
            "interact.sh", "oast.fun", "oast.pro", "oast.live", "oast.site", "oast.online", "oast.me", "burpcollaborator.net",
            "canarytokens.com", "canarytokens.org", "dnslog.cn", "ceye.io", "bxss.me", "xss.ht", "ezxss.com",
            "ngrok.io", "ngrok-free.app", "ngrok.app", "trycloudflare.com", "serveo.net", "serveousercontent.com",
            "loca.lt", "localhost.run", "r3dir.me", "pinggy.io", "pinggy.link", "devtunnels.ms", "bore.pub",
            "httpbin.org", "httpbingo.org", "postman-echo.com", "mockbin.org", "mocky.io", "smee.io", "hookdeck.com",
        ];

        private static readonly Regex IpInHost = new (
            @"(^|[.\-])(\d{1,3})[.\-](\d{1,3})[.\-](\d{1,3})[.\-](\d{1,3})([.\-]|$)",
            RegexOptions.CultureInvariant,
            RegexTimeout);

        // An explicit port in the address, even the default one or an empty one ("host:80", "host:").
        private static readonly Regex ExplicitPort = new (
            @"^[a-z]+://[^/?#]*:\d*([/?#]|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexTimeout);

        /// <summary>True for request catchers, tunnels, rebinding services and names that spell out an IP address.</summary>
        /// <returns>True for a testing host.</returns>
        public static bool IsTestingHost(string? host)
        {
            var h = (host ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();
            if (h.Length == 0)
            {
                return false;
            }

            try
            {
                return TestingZones.Any(z => h == z || h.EndsWith("." + z, StringComparison.Ordinal)) || IpInHost.IsMatch(h);
            }
            catch (RegexMatchTimeoutException)
            {
                return true;
            }
        }

        /// <summary>True when an email address sits on a reserved example/test domain.</summary>
        /// <returns>True for a reserved email domain.</returns>
        public static bool IsReservedEmail(string? email)
        {
            var value = (email ?? string.Empty).Trim();
            var at = value.LastIndexOf('@');
            return at >= 0 && at < value.Length - 1 && IsReservedHost(value[(at + 1)..]);
        }

        /// <summary>
        /// Shape rules for a link on a public form. A listing link is a normal website address:
        /// a domain name, no sign-in part, no unusual port. Tor and I2P addresses may carry a port.
        /// Besides stopping junk, this keeps the "does the link load" check from being pointed at
        /// arbitrary addresses and ports.
        /// </summary>
        /// <returns>A reason to show the visitor, or null when the link is acceptable.</returns>
        public static string? CheckLink(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)
                || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return null; // empty or malformed values are reported by the normal URL validation
            }

            var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
            var hidden = host.EndsWith(".onion", StringComparison.Ordinal) || host.EndsWith(".i2p", StringComparison.Ordinal);

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                return "Links can't contain a username or password (the part before \"@\").";
            }

            if (uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
                || IPAddress.TryParse(host, out _)
                || host.All(c => char.IsDigit(c) || c == '.'))
            {
                return "Links must use a domain name, not a numeric IP address.";
            }

            if (!host.Contains('.', StringComparison.Ordinal))
            {
                return "Links must use a full domain name (for example https://shop.example.com).";
            }

            if (IsReservedHost(host))
            {
                return "This is a reserved example/test domain, not a real website.";
            }

            if (IsTestingHost(host))
            {
                return "This address belongs to a testing service, not a real website.";
            }

            if (uri.Host.EndsWith('.') || url.Trim().Contains("\\", StringComparison.Ordinal))
            {
                return "Enter the site's normal web address.";
            }

            if (!hidden && (!uri.IsDefaultPort || ExplicitPort.IsMatch(url.Trim())))
            {
                return "Links can't include a port number. Use the site's normal web address.";
            }

            return null;
        }
    }
}
