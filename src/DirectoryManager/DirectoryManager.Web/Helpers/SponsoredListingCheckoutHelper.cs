using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Pure-static helpers shared by SponsoredListingController and SponsoredListingCheckoutController.
    /// No dependencies — safe to call from both controllers without coupling them.
    /// </summary>
    internal static class SponsoredListingCheckoutHelper
    {
        internal static readonly PaymentStatus[] HoldExtendingStatuses =
        {
            PaymentStatus.InvoiceCreated,
            PaymentStatus.Pending,
            PaymentStatus.UnderPayment,
        };

        internal static readonly IReadOnlyDictionary<PaymentStatus, int> StatusRank =
            new Dictionary<PaymentStatus, int>
            {
                { PaymentStatus.Unknown,        0 },
                { PaymentStatus.InvoiceCreated, 1 },
                { PaymentStatus.UnderPayment,   2 },
                { PaymentStatus.Pending,        3 },
                { PaymentStatus.Paid,           4 },
                { PaymentStatus.Expired,        5 },
                { PaymentStatus.Failed,         5 },
                { PaymentStatus.Refunded,       5 },
                { PaymentStatus.Canceled,       5 },
                { PaymentStatus.Test,           5 },
            };

        internal static int GetMaxSlotsForType(SponsorshipType type) => type switch
        {
            SponsorshipType.MainSponsor        => Common.Constants.IntegerConstants.MaxMainSponsoredListings,
            SponsorshipType.CategorySponsor    => Common.Constants.IntegerConstants.MaxCategorySponsoredListings,
            SponsorshipType.SubcategorySponsor => Common.Constants.IntegerConstants.MaxSubcategorySponsoredListings,
            _                                  => 0,
        };

        internal static bool IsTerminal(PaymentStatus s) =>
            s is PaymentStatus.Paid or PaymentStatus.Expired or PaymentStatus.Failed
              or PaymentStatus.Refunded or PaymentStatus.Canceled or PaymentStatus.Test;

        internal static bool CanPurchaseListing(int totalActive, int totalReserved, SponsorshipType type)
        {
            var max = GetMaxSlotsForType(type);
            return totalActive <= max && totalReserved < (max - totalActive);
        }

        internal static PaymentStatus ConvertToInternalStatus(NowPayments.API.Enums.PaymentStatus s) => s switch
        {
            NowPayments.API.Enums.PaymentStatus.Unknown      => PaymentStatus.Unknown,
            NowPayments.API.Enums.PaymentStatus.Waiting      => PaymentStatus.InvoiceCreated,
            NowPayments.API.Enums.PaymentStatus.Sending
                or NowPayments.API.Enums.PaymentStatus.Confirming
                or NowPayments.API.Enums.PaymentStatus.Confirmed => PaymentStatus.Pending,
            NowPayments.API.Enums.PaymentStatus.Finished     => PaymentStatus.Paid,
            NowPayments.API.Enums.PaymentStatus.PartiallyPaid => PaymentStatus.UnderPayment,
            NowPayments.API.Enums.PaymentStatus.Failed
                or NowPayments.API.Enums.PaymentStatus.Refunded => PaymentStatus.Failed,
            NowPayments.API.Enums.PaymentStatus.Expired      => PaymentStatus.Expired,
            _ => throw new ArgumentOutOfRangeException(nameof(s), s, null),
        };

        /// <summary>Resolves the integer ID used to key reservation groups and capacity checks.</summary>
        internal static int ResolveTypeIdForGroup(
            SponsorshipType type,
            DirectoryEntry entry,
            int? subCategoryId,
            int? categoryId) => type switch
        {
            SponsorshipType.MainSponsor        => 0,
            SponsorshipType.CategorySponsor    => categoryId ?? entry.SubCategory?.CategoryId ?? 0,
            SponsorshipType.SubcategorySponsor => subCategoryId ?? entry.SubCategoryId,
            _                                  => 0,
        };
    }
}
