using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.ReviewNotifier
{
    /// <summary>
    /// Delivers one outbox row with at-most-once semantics, so a listing owner is never emailed
    /// twice for the same review or reply:
    ///   1. claim the row (Status = Sending) and SAVE before sending;
    ///   2. send;
    ///   3. record Sent (or, if the provider rejected it, back to Pending / Failed) and save.
    /// If step 3's save fails after a successful send, the row stays Sending and is never retried.
    /// </summary>
    public static class NotificationDelivery
    {
        /// <summary>Delivers one notification row. The save delegate persists the row's current state.</summary>
        /// <returns>True when the email was handed to the provider.</returns>
        public static async Task<bool> DeliverAsync(
            ReviewNotification n,
            Func<Task> send,
            Func<Task> save,
            int maxAttempts,
            Action<string>? log = null)
        {
            n.AttemptCount++;
            n.Status = ReviewNotificationStatus.Sending;
            n.UpdateDate = DateTime.UtcNow;
            await save();

            bool sent;
            try
            {
                await send();
                n.Status = ReviewNotificationStatus.Sent;
                n.SentUtc = DateTime.UtcNow;
                n.LastError = null;
                sent = true;
                log?.Invoke($"  sent {n.NotificationType} #{n.ReviewNotificationId} to {n.RecipientEmail}");
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                n.LastError = msg.Length > 1000 ? msg.Substring(0, 1000) : msg;

                // The provider rejected it, so nothing went out: back to Pending to retry next run
                // (or Failed once out of attempts).
                n.Status = n.AttemptCount >= maxAttempts
                    ? ReviewNotificationStatus.Failed
                    : ReviewNotificationStatus.Pending;
                sent = false;
                log?.Invoke($"  FAILED #{n.ReviewNotificationId} attempt {n.AttemptCount}: {msg}");
            }

            n.UpdateDate = DateTime.UtcNow;
            await save();
            return sent;
        }
    }
}
