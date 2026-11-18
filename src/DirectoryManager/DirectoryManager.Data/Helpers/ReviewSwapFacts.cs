using System.Globalization;

namespace DirectoryManager.Data.Helpers
{
    /// <summary>
    /// Two optional facts a reviewer of an exchange can add to a review: how long the swap took
    /// and how many confirmations the exchange waited for. Both are plain numbers the reviewer
    /// states; nothing checks them. They are collected only for listings in the Exchanges
    /// category and shown as one quiet line under the review.
    /// </summary>
    public static class ReviewSwapFacts
    {
        /// <summary>Longest processing time accepted: 30 days, in minutes.</summary>
        public const int MaxMinutes = 43200;

        /// <summary>Most confirmations accepted.</summary>
        public const int MaxConfirmations = 1000;

        public const string ExchangesCategoryKey = "exchanges";

        /// <summary>True for the Exchanges category (swaps, aggregators, CEX, P2P ...).</summary>
        /// <returns>Whether the two fields are offered for a listing in this category.</returns>
        public static bool AppliesToCategory(string? categoryKey) =>
            string.Equals(categoryKey?.Trim(), ExchangesCategoryKey, StringComparison.OrdinalIgnoreCase);

        /// <summary>Minutes kept only when between 1 and <see cref="MaxMinutes"/>; anything else is "not stated".</summary>
        /// <returns>The value to store.</returns>
        public static int? CleanMinutes(int? minutes) =>
            minutes is >= 1 and <= MaxMinutes ? minutes : null;

        /// <summary>Confirmations kept only when between 0 and <see cref="MaxConfirmations"/>.</summary>
        /// <returns>The value to store.</returns>
        public static int? CleanConfirmations(int? confirmations) =>
            confirmations is >= 0 and <= MaxConfirmations ? confirmations : null;

        /// <summary>"25 min", "1 h 20 min", "3 h", "2 days", "1 day 4 h".</summary>
        /// <returns>The duration in words, or null when not stated.</returns>
        public static string? FormatDuration(int? minutes)
        {
            if (CleanMinutes(minutes) is not int m)
            {
                return null;
            }

            if (m < 60)
            {
                return m.ToString(CultureInfo.InvariantCulture) + " min";
            }

            if (m < 1440)
            {
                var h = m / 60;
                var rest = m % 60;
                return rest == 0 ? $"{h} h" : $"{h} h {rest} min";
            }

            var days = m / 1440;
            var hours = (m % 1440) / 60;
            var dayWord = days == 1 ? "day" : "days";
            return hours == 0 ? $"{days} {dayWord}" : $"{days} {dayWord} {hours} h";
        }

        /// <summary>"1 confirmation", "10 confirmations", "0 confirmations".</summary>
        /// <returns>The count in words, or null when not stated.</returns>
        public static string? FormatConfirmations(int? confirmations) =>
            CleanConfirmations(confirmations) is int c
                ? c.ToString(CultureInfo.InvariantCulture) + (c == 1 ? " confirmation" : " confirmations")
                : null;

        /// <summary>
        /// The one-line summary shown under a review: "Processing time: 25 min · 10 confirmations".
        /// Null when the reviewer stated neither.
        /// </summary>
        /// <returns>The line, or null.</returns>
        public static string? Summary(int? minutes, int? confirmations)
        {
            var time = FormatDuration(minutes);
            var confs = FormatConfirmations(confirmations);
            if (time == null && confs == null)
            {
                return null;
            }

            if (time == null)
            {
                return confs;
            }

            return confs == null ? "Processing time: " + time : $"Processing time: {time} · {confs}";
        }
    }
}
