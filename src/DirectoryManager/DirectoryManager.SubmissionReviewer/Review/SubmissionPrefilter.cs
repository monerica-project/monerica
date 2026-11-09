using System.Text.RegularExpressions;
using DirectoryManager.Data.Models;
using DirectoryManager.SubmissionReviewer.Fetching;
using DirectoryManager.SubmissionReviewer.Text;

namespace DirectoryManager.SubmissionReviewer.Review
{
    public enum PrefilterOutcome
    {
        PassToAi,
        AutoReject,
        Flag,
        Skip,
    }

    public sealed record PrefilterDecision(PrefilterOutcome Outcome, string Reason)
    {
        public override string ToString() => $"{this.Outcome}: {this.Reason}";
    }

    /// <summary>An existing listing, reduced to what duplicate checks need.</summary>
    public sealed record ExistingListing(int Id, string Name, string Link, string? Link2, string? Link3, bool IsRemoved);

    /// <summary>
    /// Cheap code-only checks that run before any AI call and can short-circuit it.
    /// Auto-reject only MARKS the submission; an admin confirms every reject.
    /// </summary>
    public static class SubmissionPrefilter
    {
        public const string SiteCheckerMarker = "(automated submission)";

        // Categories Monerica doesn't sell sponsorships to: never auto-handled, always an admin decision.
        private static readonly (string Category, Regex Pattern)[] BannedCategories =
        {
            ("gambling", new Regex(@"\b(casino|casinos|gambl\w*|sportsbook|betting|bet\s?slip|poker|roulette|blackjack|slot\s?machines?|crash\s?game|dice\s?game|lottery|jackpot)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
            ("medical/pharmaceutical", new Regex(@"\b(pharmacy|pharmac\w+|prescription|rx\b|viagra|cialis|tramadol|oxycodone|xanax|adderall|steroids?|peptides?|sarms?|telehealth|medications?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
            ("pornography/adult", new Regex(@"\b(porn\w*|xxx|nsfw|adult\s?(content|site|videos?)|escorts?|onlyfans|camgirls?|webcam\s?models?|hentai|sex\s?(toys?|shop|chat))\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        };

        /// <param name="submission">The submission (submitted fields are only read).</param>
        /// <param name="listings">All listings, for duplicate checks.</param>
        /// <param name="otherSubmissionLinks">Links of other Pending/Approved submissions (id, link).</param>
        /// <param name="fetch">Result of fetching the submitted link, or null when not fetched.</param>
        public static PrefilterDecision Evaluate(
            Submission submission,
            IReadOnlyList<ExistingListing> listings,
            IReadOnlyList<(int SubmissionId, string Link)> otherSubmissionLinks,
            FetchResult? fetch)
        {
            if (submission.NoteToAdmin != null && submission.NoteToAdmin.StartsWith(SiteCheckerMarker, StringComparison.Ordinal))
            {
                return new (PrefilterOutcome.Skip, "Site-checker status proposal (reviewed by hand as before)");
            }

            var spam = SpamReason(submission);
            if (spam != null)
            {
                return new (PrefilterOutcome.AutoReject, "Spam: " + spam);
            }

            var key = UrlNormalizer.Normalize(submission.Link);
            var dup = listings.FirstOrDefault(l =>
                l.Id != submission.DirectoryEntryId &&
                (UrlNormalizer.Normalize(l.Link) == key ||
                 (!string.IsNullOrWhiteSpace(l.Link2) && UrlNormalizer.Normalize(l.Link2) == key) ||
                 (!string.IsNullOrWhiteSpace(l.Link3) && UrlNormalizer.Normalize(l.Link3) == key)));
            if (dup != null)
            {
                return dup.IsRemoved
                    ? new (PrefilterOutcome.Flag, $"Same URL as removed listing \"{dup.Name}\" (#{dup.Id}): re-listing request?")
                    : new (PrefilterOutcome.AutoReject, $"Already listed as \"{dup.Name}\" (#{dup.Id})");
            }

            var dupSub = otherSubmissionLinks.FirstOrDefault(s =>
                s.SubmissionId != submission.SubmissionId && UrlNormalizer.Normalize(s.Link) == key);
            if (dupSub.SubmissionId != 0)
            {
                return new (PrefilterOutcome.AutoReject, $"Same URL already submitted (submission #{dupSub.SubmissionId})");
            }

            if (fetch != null && !fetch.Reachable)
            {
                return new (PrefilterOutcome.AutoReject, $"URL unreachable after one retry ({fetch.Error ?? "no response"})");
            }

            var banned = BannedCategory(submission);
            if (banned != null)
            {
                return new (PrefilterOutcome.Flag, $"Possible banned sponsorship category: {banned}");
            }

            return new (PrefilterOutcome.PassToAi, fetch?.Blocked == true
                ? $"Passed (site blocks bots: HTTP {fetch.StatusCode})"
                : "Passed");
        }

        /// <summary>The banned sponsorship category the URL, name or description matches, or null.</summary>
        public static string? BannedCategory(Submission s)
        {
            var hay = $"{s.Link} {s.Name} {s.Description}";
            foreach (var (category, pattern) in BannedCategories)
            {
                var m = pattern.Match(hay);
                if (m.Success)
                {
                    return $"{category} (\"{m.Value}\")";
                }
            }

            return null;
        }

        /// <summary>Why the submission looks like spam, or null.</summary>
        public static string? SpamReason(Submission s)
        {
            var name = (s.Name ?? string.Empty).Trim();
            var desc = (s.Description ?? string.Empty).Trim();

            if (name.Length == 0)
            {
                return "empty name";
            }

            if (string.IsNullOrWhiteSpace(s.Link))
            {
                return "empty link";
            }

            // An edit of an existing listing may leave the description as it is.
            if (s.DirectoryEntryId is null)
            {
                if (desc.Length < 15)
                {
                    return desc.Length == 0 ? "empty description" : $"description too short ({desc.Length} characters)";
                }

                var letters = desc.Count(char.IsLetter);
                if (letters < desc.Length * 0.5)
                {
                    return "description is mostly symbols/numbers";
                }

                if (Regex.IsMatch(desc, @"(.)\1{7,}"))
                {
                    return "description is repeated characters";
                }

                var words = Regex.Matches(desc.ToLowerInvariant(), @"[a-z]{3,}").Select(m => m.Value).ToList();
                if (words.Count >= 6 && words.Distinct().Count() <= words.Count / 3)
                {
                    return "description repeats the same words";
                }
            }

            var urls = HtmlText.CountUrls(desc) + HtmlText.CountUrls(s.Note);
            if (urls >= 3)
            {
                return $"link farm ({urls} URLs in the description/note)";
            }

            return null;
        }
    }
}
