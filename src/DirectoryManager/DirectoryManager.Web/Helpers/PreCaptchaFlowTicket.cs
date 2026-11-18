using System.Globalization;
using System.Security.Cryptography;
using DirectoryManager.Web.Constants;
using Microsoft.AspNetCore.DataProtection;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Carries the pre-captcha step of a public multi-step flow (review, reply, my-reviews,
    /// problem report, verification request) without any server-side state.
    ///
    /// "Begin" used to put a flow entry in IMemoryCache before the visitor had solved a captcha,
    /// so an anonymous flood of begin requests grew memory without bound until the entries
    /// expired. Now begin only mints a signed, time-limited ticket (flow id + the one or two ids
    /// the flow needs) that travels in the captcha page's URL and hidden field; the server-side
    /// flow is created only once the captcha has been solved. Nothing is stored per ticket, so
    /// begin requests cost no memory however many arrive, and no visitor IP is involved.
    /// </summary>
    public static class PreCaptchaFlowTicket
    {
        /// <summary>The form/query field the ticket travels in.</summary>
        public const string FieldName = "ticket";

        // A ticket is ~150 chars; anything far longer is junk and is rejected before decoding.
        private const int MaxTicketChars = 1024;

        private const string PurposePrefix = "DirectoryManager.Web.PreCaptchaFlow.v1:";

        // Generous: the captcha page may sit open a while. Costs nothing (no server state).
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(IntegerConstants.ComposeFlowExpiresMinutes);

        public static string Issue(IDataProtectionProvider provider, string flowKind, Guid flowId, int idA = 0, int idB = 0)
        {
            var payload = string.Join(
                '|',
                flowId.ToString("N"),
                idA.ToString(CultureInfo.InvariantCulture),
                idB.ToString(CultureInfo.InvariantCulture));

            return Protector(provider, flowKind).Protect(payload, Lifetime);
        }

        /// <summary>True when <paramref name="ticket"/> is a valid, unexpired ticket of this flow
        /// kind issued for <paramref name="flowId"/>.</summary>
        public static bool TryRead(
            IDataProtectionProvider provider,
            string flowKind,
            string? ticket,
            Guid flowId,
            out int idA,
            out int idB)
        {
            idA = 0;
            idB = 0;

            if (string.IsNullOrWhiteSpace(ticket) || ticket.Length > MaxTicketChars || flowId == Guid.Empty)
            {
                return false;
            }

            string payload;
            try
            {
                payload = Protector(provider, flowKind).Unprotect(ticket.Trim(), out _);
            }
            catch (CryptographicException)
            {
                return false; // tampered, expired, or issued for another flow kind
            }
            catch (FormatException)
            {
                return false;
            }

            var parts = payload.Split('|');
            if (parts.Length != 3 ||
                !Guid.TryParseExact(parts[0], "N", out var ticketFlowId) ||
                ticketFlowId != flowId ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out idA) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out idB))
            {
                idA = 0;
                idB = 0;
                return false;
            }

            return true;
        }

        private static ITimeLimitedDataProtector Protector(IDataProtectionProvider provider, string flowKind) =>
            provider.CreateProtector(PurposePrefix + flowKind).ToTimeLimitedDataProtector();
    }
}
