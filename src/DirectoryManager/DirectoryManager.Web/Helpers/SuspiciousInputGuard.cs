using System.Net;
using System.Text.RegularExpressions;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Recognises SQL-injection and script-injection probes in request input (query strings).
    /// Every query in the app is parameterised and every view encodes its output, so these
    /// probes never did anything; this guard exists so they are refused outright with a 400
    /// instead of being served a normal page, logged as searches, or reaching the database.
    /// Patterns are deliberately specific to attack syntax so ordinary searches
    /// (apostrophes, "and"/"or", product names) are never rejected.
    /// </summary>
    public static partial class SuspiciousInputGuard
    {
        /// <summary>Longest single query-string value the site ever needs.</summary>
        public const int MaxValueLength = 1024;

        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

        private static readonly (string Name, Regex Pattern)[] Patterns =
        {
            ("sql-union-select", Build(@"\bunion\b[\s(]+(all[\s(]+)?select\b")),
            ("sql-select-from", Build(@"\bselect\b\s+(\*|distinct\b|top\s+\d|null\b|\w+\s*\(|[\w.""`]+\s*,).{0,200}?\bfrom\b")),
            ("sql-stacked-statement", Build(@";\s*(drop|select|insert|update|delete|exec|execute|declare|shutdown|truncate|alter|create)\b")),
            // quote, boolean, then a comparison soon after (' or '1'='1) — not plain quoted
            // search terms like "monero" or "tor".
            ("sql-quote-boolean", Build(@"['""`]\s*\)*\s*(or|and|xor|\|\||&&)\s*[('""`\d][^=<>]{0,40}(=|<>|!=|<|>|\blike\b)")),
            ("sql-tautology", Build(@"\b(or|and|xor)\b\s*\(?\s*['""`]?\w{1,20}['""`]?\s*(=|<>|!=|\blike\b)\s*['""`]?\w{1,20}['""`]?\s*(--|#|/\*|$|\))")),
            ("sql-numeric-tautology", Build(@"\b(or|and)\s+\d+(\s*[*+\-/]\s*\d+)*\s*=\s*\d+")),
            ("sql-quote-terminator", Build(@"['""`]\s*\)+|['""`]\s*(--|#|;)|\)\s*--")),
            ("sql-comment", Build(@"/\*.*?\*/|--\s*-?\s*$|--\+")),
            ("sql-time-delay", Build(@"\b(pg_sleep|sleep|benchmark|dbms_pipe\.receive_message|dbms_lock\.sleep)\s*\(|\bwaitfor\s+delay\b")),
            ("sql-function", Build(@"\b(extractvalue|updatexml|load_file|concat_ws|group_concat|string_agg|current_database|current_user|version|user|database|chr|char|ascii|substring|substr|convert|cast|elt|ifnull|nullif|utl_inaddr\.get_host_name|xp_cmdshell)\s*\(")),
            ("sql-metadata", Build(@"\b(information_schema|pg_catalog|pg_user|pg_shadow|sysobjects|syscolumns|sys\.tables|all_tables|sqlite_master|mysql\.user)\b|@@\w+")),
            ("sql-into-outfile", Build(@"\binto\s+(out|dump)file\b|\bcopy\b.{0,60}\b(to|from)\s+program\b")),
            ("sql-hex-literal", Build(@"([,(=*]|\bthen|\belse)\s*0x[0-9a-f]{6,}\b")),
            ("sql-order-by-probe", Build(@"['""`)\d]\s*\border\s+by\s+\d+|\border\s+by\s+\d+\s*(--|#)")),
            ("script-tag", Build(@"<\s*/?\s*(script|svg|img|iframe|object|embed|body|style|link|meta|math|details|video|audio|form|input|textarea|title|base)\b")),
            ("angle-bracket", Build(@"[<>]")),
            ("script-handler", Build(@"(?<![a-z])on(load|error|click|mouseover|focus|toggle|begin|start|animationstart|pointerover)\s*=")),
            ("script-scheme", Build(@"\b(javascript|vbscript)\s*:|\bdata\s*:\s*text/html")),
            ("sql-subquery", Build(@"\(\s*select\b|\bcase\s+when\b.{0,120}?\bthen\b|\b(rlike|regexp)\s*\(|\bfrom\s+dual\b|\bprocedure\s+analyse\b")),
            ("script-attribute", Build(@"['""]\s*[\w-]+\s*=\s*[\w.]+\s*\(|\\u00(22|27|3c|3e)|\bexpression\s*\(")),
            ("code-injection", Build(@"\b(response\.write|phpinfo|passthru|shell_exec|system|exec|eval|assert|print|md5|base64_decode|file_get_contents)\s*\(")),
            // | ; ` and && followed by any of these commands; a lone & only before clearly hostile
            // commands (so "Dog & Cat", "Web & PHP" and "...&type=..." in a ReturnUrl are allowed).
            ("command-injection", Build(@"([|;`]|&&)\s*\(?\s*(nslookup|curl|wget|ping|echo|cat|whoami|id|uname|sleep|powershell|cmd|bash|sh|nc|ncat|python\d?|perl|php|env|set|dir|type)\b|&\s*\(?\s*(nslookup|curl|wget|ping|whoami|uname|powershell|bash|ncat)\b|\$\(|\$\{?ifs\b")),
            ("out-of-band-probe", Build(@"\b(bxss\.me|oastify\.com|burpcollaborator\.net|interact\.sh|interactsh\.com|dnslog\.cn|ceye\.io|oast\.(fun|pro|live|site|online|me))\b")),
            ("overlong-encoding", Build(@"%c0%[0-9a-f]{2}|%c1%[0-9a-f]{2}|%e0%80%|%f0%80%80%")),
            ("template-injection", Build(@"\$\{.{0,80}\}|\{\{.{0,80}\}\}|<%|%>")),
            ("path-traversal", Build(@"(\.\./){2,}|(\.\.\\){2,}|/etc/passwd|\bwin\.ini\b|\b[a-z]:[\\/]+windows\b|::\$data\b|\bfile:///")),
            ("control-characters", Build(@"[\x00-\x08\x0B\x0C\x0E-\x1F]")),
        };

        /// <summary>
        /// Returns the name of the first attack pattern found in <paramref name="value"/>, or null.
        /// The value is also checked after percent-decoding up to twice, because probes are
        /// often double-encoded to slip past filters.
        /// </summary>
        public static string? FindViolation(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            if (value.Length > MaxValueLength)
            {
                return "value-too-long";
            }

            var current = value;
            for (var pass = 0; pass < 3; pass++)
            {
                var hit = MatchAny(current);
                if (hit is not null)
                {
                    return hit;
                }

                string decoded;
                try
                {
                    decoded = WebUtility.UrlDecode(current);
                }
                catch (ArgumentException)
                {
                    return "malformed-encoding";
                }

                if (string.Equals(decoded, current, StringComparison.Ordinal))
                {
                    break;
                }

                current = decoded;
            }

            return null;
        }

        private static string? MatchAny(string value)
        {
            // Inline comments ("un/**/ion") and '+' used as a space are common filter evasions.
            var normalized = InlineComment().Replace(value, " ").Replace('+', ' ');

            foreach (var (name, pattern) in Patterns)
            {
                try
                {
                    if (pattern.IsMatch(value) || pattern.IsMatch(normalized))
                    {
                        return name;
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    // Input engineered to stall the matcher is itself hostile.
                    return "pattern-timeout";
                }
            }

            return null;
        }

        private static Regex Build(string pattern) =>
            new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled, MatchTimeout);

        [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline, 100)]
        private static partial Regex InlineComment();
    }
}
