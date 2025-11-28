using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DirectoryManager.Data.Enums;
using DirectoryManager.Services.Models.TransferModels;

namespace DirectoryManager.Web.Services.Interfaces
{
    /// <summary>
    /// Computes advertiser churn metrics over time windows and month-by-month series.
    /// </summary>
    public interface IChurnService
    {
        /// <summary>
        /// Compute churn metrics for an arbitrary UTC window [<paramref name="windowStartUtc"/>, <paramref name="windowEndOpenUtc"/>).
        /// </summary>
        /// <param name="windowStartUtc">Inclusive window start (UTC).</param>
        /// <param name="windowEndOpenUtc">Exclusive window end (UTC).</param>
        /// <param name="sponsorshipType">Optional filter by sponsorship type.</param>
        /// <param name="subCategoryId">Optional filter by subcategory id.</param>
        /// <param name="categoryId">Optional filter by category id.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Churn metrics for the window.</returns>
        Task<ChurnMetrics> GetChurnForWindowAsync(
            DateTime windowStartUtc,
            DateTime windowEndOpenUtc,
            SponsorshipType? sponsorshipType = null,
            int? subCategoryId = null,
            int? categoryId = null,
            CancellationToken ct = default);

        /// <summary>
        /// Compute churn metrics for a single calendar month (UTC).
        /// Interpret the month as [<paramref name="monthStartUtc"/>, next-month-start).
        /// </summary>
        /// <param name="monthStartUtc">Month start in UTC (e.g., 2025-10-01 00:00:00Z).</param>
        /// <param name="sponsorshipType">Optional filter by sponsorship type.</param>
        /// <param name="subCategoryId">Optional filter by subcategory id.</param>
        /// <param name="categoryId">Optional filter by category id.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Churn metrics for the month.</returns>
        Task<ChurnMetrics> GetMonthlyChurnAsync(
            DateTime monthStartUtc,
            SponsorshipType? sponsorshipType = null,
            int? subCategoryId = null,
            int? categoryId = null,
            CancellationToken ct = default);

        /// <summary>
        /// Compute a month-by-month churn series over [<paramref name="startMonthUtc"/>, <paramref name="endMonthOpenUtc"/>),
        /// where both arguments are expected to be canonical month starts in UTC.
        /// </summary>
        /// <param name="startMonthUtc">Inclusive first month start (UTC).</param>
        /// <param name="endMonthOpenUtc">Exclusive end month start (UTC).</param>
        /// <param name="sponsorshipType">Optional filter by sponsorship type.</param>
        /// <param name="subCategoryId">Optional filter by subcategory id.</param>
        /// <param name="categoryId">Optional filter by category id.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Ordered list of monthly churn points.</returns>
        Task<IReadOnlyList<ChurnMetricsPoint>> GetMonthlyChurnSeriesAsync(
            DateTime startMonthUtc,
            DateTime endMonthOpenUtc,
            SponsorshipType? sponsorshipType = null,
            int? subCategoryId = null,
            int? categoryId = null,
            CancellationToken ct = default);
    }
}
