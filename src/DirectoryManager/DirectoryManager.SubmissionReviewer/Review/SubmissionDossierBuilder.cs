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
            int maxChars = 6000)
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
            Field(sb, "Location", s.Location);
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
