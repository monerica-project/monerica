using System.ComponentModel;

namespace NowPayments.API.Enums
{
    public enum PaymentStatus
    {
        [Description("unknown")]
        Unknown = 0,

        /// <summary>
        /// Waiting for the customer to send the payment. The initial status of each payment.
        /// </summary>
        [Description("waiting")]
        Waiting = 1,

        /// <summary>
        /// The transaction is being processed on the blockchain. Appears when NOWPayments detect the funds from the user on the blockchain.
        /// </summary>
        [Description("confirming")]
        Confirming = 2,

        /// <summary>
        /// The process is confirmed by the blockchain. Customer’s funds have accumulated enough confirmations.
        /// </summary>
        [Description("confirmed")]
        Confirmed = 3,

        /// <summary>
        /// The funds are being sent to your personal wallet. We are in the process of sending the funds to you.
        /// </summary>
        [Description("sending")]
        Sending = 4,

        /// <summary>
        /// It shows that the customer sent less than the actual price. Appears when the funds have arrived in your wallet.
        /// </summary>
        [Description("partially_paid")]
        PartiallyPaid = 5,

        /// <summary>
        /// The funds have reached your personal address and the payment is finished.
        /// </summary>
        [Description("finished")]
        Finished = 6,

        /// <summary>
        /// The payment wasn't completed due to an error of some kind.
        /// </summary>
        [Description("failed")]
        Failed = 7,

        /// <summary>
        /// The funds were refunded back to the user.
        /// </summary>
        [Description("refunded")]
        Refunded = 8,

        /// <summary>
        /// The user didn't send the funds to the specified address in the 24-hour time window.
        /// </summary>
        [Description("expired")]
        Expired = 9
    }
}