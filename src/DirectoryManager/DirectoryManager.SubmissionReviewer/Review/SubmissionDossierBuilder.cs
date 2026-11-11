using System.Text;
using DirectoryManager.Data.Models;
using DirectoryManager.SubmissionReviewer.Fetching;
using DirectoryManager.SubmissionReviewer.Text;

namespace DirectoryManager.SubmissionReviewer.Review
{
    /// <summary>The listing a submission edits, as it is live now.</summary>
    public sealed record CurrentListing(
        int Id,
        string Name,
        string Link,
        string? Description,
        string? Note,
        string Subcategory,
        string Status,
        string? KycPolicy,
        string? CountryCode);

    /// <summary>
    /// Builds the compact text the model judges ("dossier"): the submitter's fields, what the
    /// site itself says (title, meta, Monero/payment lines, start of the body; at most 2 pages),
    /// the live listing for an edit, and the most similar existing listings. Target ≤ 6k chars.
    /// </summary>
    public sealed class SubmissionDossierBuilder
    {
        private readonly IPageFetcher fetcher;

        public SubmissionDossierBuilder(IPageFetcher fetcher) => this.fetcher = fetcher;

        /// <summary>Fetches the submitted page and (if found) one Monero/payment page linked from it.</summary>
        public async Task<(FetchResult Main, FetchResult? Second)> FetchPagesAsync(string link, string? proofLink, CancellationToken ct)
        {
            var main = await this.fetcher.FetchAsync(link, ct);
            FetchResult? second = null;

            // The submitter's proof link (their own checkout/payment/FAQ page) beats a guessed one.
            if (!string.IsNullOrWhiteSpace(proofLink) &&
                UrlNormalizer.Normalize(proofLink) != UrlNormalizer.Normalize(link))
            {
                second = await this.fetcher.FetchAsync(proofLink, ct);
            }
            else if (main.Html != null && main.FinalUri != null)
            {
                var payLink = HtmlText.FindPaymentLink(main.Html, main.FinalUri);
                if (payLink != null)
                {
                    second = await this.fetcher.FetchAsync(payLink, ct);
                }
            }

            return (main, second);
        }

        /// <summary>
        /// Looks for a referral/affiliate program the plain fetch can't see: links on the page, and
        /// routes in the site's own script files (menus built by JavaScript). Returns evidence lines.
        /// </summary>
        public async Task<IReadOnlyList<string>> FindReferralProgramAsync(FetchResult main, CancellationToken ct)
        {
            var found = new List<string>();
            if (main.Html == null || main.FinalUri == null)
            {
                return found;
            }

            foreach (var link in ReferralScan.LinksInHtml(main.Html, main.FinalUri))
            {
                found.Add($"Link on the page: {link}");
            }

            foreach (var scriptUrl in ReferralScan.ScriptUrls(main.Html, main.FinalUri))
            {
                var script = await this.fetcher.FetchAsync(scriptUrl, ct);
                var routes = ReferralScan.RoutesInScript(script.Html);
                if (routes.Count > 0)
                {
                    found.Add($"The site's own JavaScript (menu/pages a plain fetch doesn't run) has the route(s): {string.Join(", ", routes)}");
                    break;
                }
            }

            return found;
        }

        /// <summary>
        /// The site's own social / messenger profiles, from page links and its own script files
        /// (JavaScript-built footers). Returns dossier lines.
        /// </summary>
        public async Task<IReadOnlyList<string>> FindSocialLinksAsync(FetchResult main, CancellationToken ct)
        {
            var found = new List<string>();
            if (main.Html == null || main.FinalUri == null)
            {
                return found;
            }

            var contents = new List<string?> { main.Html };
            foreach (var scriptUrl in ReferralScan.ScriptUrls(main.Html, main.FinalUri))
            {
                contents.Add((await this.fetcher.FetchAsync(scriptUrl, ct)).Html);
            }

            var (social, messenger) = SocialLinkScan.Find(contents.ToArray());
            if (social.Count > 0)
            {
                found.Add("Social profiles: " + string.Join(", ", social));
            }

            if (messenger.Count > 0)
            {
                found.Add("Messenger / chat: " + string.Join(", ", messenger));
            }

            return found;
        }

        /// <summary>
        /// KYC evidence beyond the two fetched pages: the site's own terms / FAQ / AML-KYC pages
        /// (Link 2/3 first, then legal links on the page) and the KYC/ID sentences on all of them,
        /// so a "No KYC" line or a "we may freeze funds and ask for ID" clause is never cut off by
        /// the body-text budget. Returns dossier lines.
        /// </summary>
        public async Task<IReadOnlyList<string>> FindKycStatementsAsync(Submission s, FetchResult main, FetchResult? second, CancellationToken ct)
        {
            var found = new List<string>();
            void AddFrom(string label, string? html)
            {
                foreach (var line in SiteSignalScan.KycSentences(html))
                {
                    found.Add($"{label}: \"{line}\"");
                }
            }

            AddFrom(main.FinalUri?.ToString() ?? s.Link, main.Html);
            if (second?.Html != null)
            {
                AddFrom(second.FinalUri?.ToString() ?? second.Url, second.Html);
            }

            if (main.Html == null || main.FinalUri == null)
            {
                return found;
            }

            var already = new[] { main.FinalUri?.ToString(), second?.FinalUri?.ToString(), second?.Url }
                .Where(u => u != null).Select(UrlNormalizer.Normalize).ToHashSet(StringComparer.Ordinal);
            foreach (var url in SiteSignalScan.LegalPages(main.Html, main.FinalUri, new[] { s.Link2, s.Link3 }))
            {
                if (already.Contains(UrlNormalizer.Normalize(url)))
                {
                    continue;
                }

                var page = await this.fetcher.FetchAsync(url, ct);
                if (page.Html == null)
                {
                    found.Add($"{url}: (could not be fetched: {page.Error ?? "no HTML"})");
                    continue;
                }

                var before = found.Count;
                AddFrom(page.FinalUri?.ToString() ?? url, page.Html);
                if (found.Count == before)
                {
                    found.Add($"{url}: (fetched; no sentence about KYC, ID checks or holding funds)");
                }
            }

            return found.Take(16).ToList();
        }

        /// <summary>Links on the page to API / developer docs. Returns dossier lines.</summary>
        public static IReadOnlyList<string> FindApiLinks(FetchResult main) =>
            main.Html == null || main.FinalUri == null
                ? Array.Empty<string>()
                : SiteSignalScan.ApiLinks(main.Html, main.FinalUri).Select(l => "Link on the page: " + l).ToList();

        public static IReadOnlyList<ExistingListing> MostSimilar(Submission s, IEnumerable<ExistingListing> listings, int take = 5)
        {
            var host = UrlNormalizer.Host(s.Link);
            return listings
                .Where(l => l.Id != s.DirectoryEntryId)
                .Select(l => (l, score: Math.Max(
                    Similarity.Trigram(s.Name, l.Name),
                    Similarity.Trigram(host, UrlNormalizer.Host(l.Link)))))
                .Where(x => x.score >= 0.25)
                .OrderByDescending(x => x.score)
                .Take(take)
                .Select(x => x.l)
                .ToList();
        }

        public static string Build(
            Submission s,
            string? mainSubcategory,
            FetchResult? main,
            FetchResult? second,
            CurrentListing? current,
            IReadOnlyList<ExistingListing> similar,
            int maxChars = 6000,
            IReadOnlyList<string>? referralEvidence = null,
            IReadOnlyList<string>? socialLinks = null,
            IReadOnlyList<string>? kycStatements = null,
            IReadOnlyList<string>? apiLinks = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Submission (as submitted)");
            sb.AppendLine(current == null ? "Type: NEW listing" : $"Type: EDIT of existing listing #{current.Id}");
            Field(sb, "Name", s.Name);
            Field(sb, "Link", s.Link);
            Field(sb, "Link2", s.Link2);
            Field(sb, "Link3", s.Link3);
            Field(sb, "Description", s.Description);
            Field(sb, "Note", s.Note);
            Field(sb, "Main subcategory", mainSubcategory);
            Field(sb, "Submitter's suggested new subcategory", s.SuggestedSubCategory);
            Field(sb, "Tags typed", s.Tags);
            Field(sb, "Street address", s.StreetAddress);
            Field(sb, "City", s.City);
            Field(sb, "State/Region", s.StateRegion);
            Field(sb, "Postal code", s.PostalCode);
            Field(sb, "Country code", s.CountryCode);
            Field(sb, "KYC policy", s.KycPolicy?.ToString());
            Field(sb, "Liquidity", s.Liquidity.ToString());
            Field(sb, "Rate type", s.RateType.ToString());
            Field(sb, "JavaScript", s.SiteJavaScript.ToString());
            Field(sb, "Processor", s.Processor);
            Field(sb, "Proof link", s.ProofLink);
            if (!string.IsNullOrWhiteSpace(s.ProofLink) && !SameSite(s.ProofLink, s.Link))
            {
                sb.AppendLine($"Check: the proof link is NOT on the submitted site's domain ({UrlNormalizer.Host(s.ProofLink)} vs {UrlNormalizer.Host(s.Link)}).");
            }

            Field(sb, "Source code", s.SourceCodeLink);
            Field(sb, "Founded", s.FoundedDate?.ToString("yyyy-MM-dd"));
            Field(sb, "Email", s.Email);
            Field(sb, "Messenger", s.Messenger);
            Field(sb, "Social", s.Social);
            Field(sb, "Note to admin (private, from the submitter)", Trim(s.NoteToAdmin, 1000));

            if (current != null)
            {
                sb.AppendLine();
                sb.AppendLine("## Live listing now (what the edit changes)");
                Field(sb, "Name", current.Name);
                Field(sb, "Link", current.Link);
                Field(sb, "Description", current.Description);
                Field(sb, "Note", current.Note);
                Field(sb, "Subcategory", current.Subcategory);
                Field(sb, "Status", current.Status);
                Field(sb, "KYC policy", current.KycPolicy);
                Field(sb, "Country code", current.CountryCode);
            }

            if (referralEvidence is { Count: > 0 })
            {
                sb.AppendLine();
                sb.AppendLine("## Referral / affiliate program check");
                foreach (var line in referralEvidence)
                {
                    sb.AppendLine("- " + line);
                }
            }

            sb.AppendLine();
            sb.AppendLine("## KYC / ID statements on the site's own pages (home, proof page, terms, FAQ, AML/KYC policy)");
            if (kycStatements is { Count: > 0 })
            {
                foreach (var line in kycStatements)
                {
                    sb.AppendLine("- " + Trim(line, 420));
                }
            }
            else
            {
                sb.AppendLine("(none found)");
            }

            if (apiLinks is { Count: > 0 })
            {
                sb.AppendLine();
                sb.AppendLine("## API / developer docs check");
                foreach (var line in apiLinks)
                {
                    sb.AppendLine("- " + line);
                }
            }

            sb.AppendLine();
            sb.AppendLine("## Social and messenger links found on the site (its page links and own scripts)");
            if (socialLinks is { Count: > 0 })
            {
                foreach (var line in socialLinks)
                {
                    sb.AppendLine("- " + line);
                }
            }
            else
            {
                sb.AppendLine("(none found)");
            }

            var fixedPart = sb.ToString();
            var similarPart = new StringBuilder();
            similarPart.AppendLine();
            similarPart.AppendLine("## Most similar existing listings");
            if (similar.Count == 0)
            {
                similarPart.AppendLine("(none)");
            }

            foreach (var l in similar)
            {
                similarPart.AppendLine($"- #{l.Id} {l.Name} — {l.Link}{(l.IsRemoved ? " (removed)" : string.Empty)}");
            }

            var budget = Math.Max(1200, maxChars - fixedPart.Length - similarPart.Length - 50);
            var pages = new StringBuilder();
            AppendPage(pages, "Submitted page", main, (int)(budget * (second == null ? 1.0 : 0.65)));
            if (second != null)
            {
                AppendPage(pages, "Proof / payment page", second, (int)(budget * 0.35));
            }

            var all = fixedPart + pages + similarPart;
            return all.Length > maxChars ? all[..maxChars] : all;
        }

        private static void AppendPage(StringBuilder sb, string label, FetchResult? page, int budget)
        {
            sb.AppendLine();
            sb.AppendLine($"## {label}: {page?.FinalUri?.ToString() ?? page?.Url ?? "(not fetched)"}");
            if (page == null)
            {
                return;
            }

            if (page.Html == null)
            {
                sb.AppendLine($"Could not read the page: {page.Error ?? "no HTML"}");
                return;
            }

            if (page.Blocked)
            {
                sb.AppendLine($"Note: the site answered HTTP {page.StatusCode} (bot protection); the text below may be a challenge page.");
            }

            var text = HtmlText.Extract(page.Html, maxBody: Math.Max(400, budget));
            var start = sb.Length;
            Field(sb, "Title", text.Title);
            Field(sb, "Meta description", text.MetaDescription);
            if (text.PaymentLines.Count > 0)
            {
                sb.AppendLine("Lines mentioning Monero/XMR/crypto/payments:");
                foreach (var l in text.PaymentLines)
                {
                    sb.AppendLine("  - " + l);
                }
            }
            else
            {
                sb.AppendLine("Lines mentioning Monero/XMR/crypto/payments: (none found on this page)");
            }

            var left = budget - (sb.Length - start);
            if (left > 200)
            {
                sb.AppendLine("Body text: " + Trim(text.Body, left));
            }
        }

        private static bool SameSite(string a, string b)
        {
            var ha = UrlNormalizer.Host(a);
            var hb = UrlNormalizer.Host(b);
            return ha == hb || ha.EndsWith("." + hb, StringComparison.Ordinal) || hb.EndsWith("." + ha, StringComparison.Ordinal) || UrlNormalizer.IsOnion(a);
        }

        private static void Field(StringBuilder sb, string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && value != "Unknown")
            {
                sb.AppendLine($"{name}: {value.Trim()}");
            }
        }

        private static string? Trim(string? s, int max) =>
            s == null ? null : s.Length <= max ? s : s[..max] + "…";
    }
}
