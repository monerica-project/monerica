using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace DirectoryManager.Web.Tests.Security
{
    /// <summary>
    /// Owner rule (2026-11-13): the site ships no JavaScript. The CSP (script-src 'none') already stops
    /// scripts from running; these tests keep them out of the source too.
    /// </summary>
    public class NoJavaScriptTests
    {
        private static readonly Regex ScriptTag = new (@"<script(?![^>]*application/ld\+json)", RegexOptions.IgnoreCase);
        private static readonly Regex InlineHandler = new (@"\son[a-z]+\s*=\s*(""|'|string\.Format)|\bonsubmit\s*=|(href|src|action|formaction)\s*=\s*[""']\s*javascript:", RegexOptions.IgnoreCase);

        [Fact]
        public void Views_HaveNoScriptTagsOrInlineHandlers()
        {
            var bad = new List<string>();
            foreach (var file in Directory.EnumerateFiles(Path.Combine(WebDir(), "Views"), "*.cshtml", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (ScriptTag.IsMatch(lines[i]) || InlineHandler.IsMatch(lines[i]))
                    {
                        bad.Add($"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
                    }
                }
            }

            Assert.True(bad.Count == 0, "JavaScript found in views:\n" + string.Join("\n", bad));
        }

        [Fact]
        public void Wwwroot_HasNoScriptFiles()
        {
            var root = Path.Combine(WebDir(), "wwwroot");
            var scripts = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".js", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".mjs", StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.True(scripts.Count == 0, "Script files in wwwroot:\n" + string.Join("\n", scripts));
        }

        [Fact] // The Identity UI package ships jQuery/Bootstrap script files that get served publicly.
        public void Project_DoesNotReferenceIdentityUi() =>
            Assert.DoesNotContain("Microsoft.AspNetCore.Identity.UI", File.ReadAllText(Path.Combine(WebDir(), "DirectoryManager.Web.csproj")));

        [Fact]
        public void ContentSecurityPolicy_ForbidsAllScripts() =>
            Assert.Contains("script-src 'none'", DirectoryManager.Web.Middleware.SecurityHeadersMiddleware.Csp);

        private static string WebDir([CallerFilePath] string thisFile = "")
        {
            var dir = new DirectoryInfo(Path.GetDirectoryName(thisFile)!);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "DirectoryManager.Web")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return Path.Combine(dir!.FullName, "DirectoryManager.Web");
        }
    }
}
