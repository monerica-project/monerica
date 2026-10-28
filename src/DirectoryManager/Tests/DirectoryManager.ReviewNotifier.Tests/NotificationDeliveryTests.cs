using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.ReviewNotifier.Tests
{
    // A listing owner must get exactly one email per new review/reply, even when the database
    // save after sending fails (the job runs every 5 minutes and would otherwise resend).
    public class NotificationDeliveryTests
    {
        private const int MaxAttempts = 5;

        [Fact]
        public async Task Claims_the_row_before_sending_then_marks_it_sent()
        {
            var n = new ReviewNotification { Status = ReviewNotificationStatus.Pending };
            var savedStatuses = new List<ReviewNotificationStatus>();
            var statusWhenSent = (ReviewNotificationStatus?)null;

            var sent = await NotificationDelivery.DeliverAsync(
                n,
                send: () => { statusWhenSent = n.Status; return Task.CompletedTask; },
                save: () => { savedStatuses.Add(n.Status); return Task.CompletedTask; },
                MaxAttempts);

            Assert.True(sent);
            Assert.Equal(ReviewNotificationStatus.Sending, statusWhenSent);
            Assert.Equal(new[] { ReviewNotificationStatus.Sending, ReviewNotificationStatus.Sent }, savedStatuses);
            Assert.Equal(1, n.AttemptCount);
            Assert.NotNull(n.SentUtc);
        }

        [Fact]
        public async Task Provider_rejection_goes_back_to_pending_for_a_retry()
        {
            var n = new ReviewNotification { Status = ReviewNotificationStatus.Pending };

            var sent = await NotificationDelivery.DeliverAsync(
                n,
                send: () => throw new InvalidOperationException("SendGrid 503"),
                save: () => Task.CompletedTask,
                MaxAttempts);

            Assert.False(sent);
            Assert.Equal(ReviewNotificationStatus.Pending, n.Status);
            Assert.Equal("SendGrid 503", n.LastError);
            Assert.Null(n.SentUtc);
        }

        [Fact]
        public async Task Last_allowed_attempt_that_fails_is_marked_failed()
        {
            var n = new ReviewNotification { Status = ReviewNotificationStatus.Pending, AttemptCount = MaxAttempts - 1 };

            await NotificationDelivery.DeliverAsync(
                n,
                send: () => throw new InvalidOperationException("rejected"),
                save: () => Task.CompletedTask,
                MaxAttempts);

            Assert.Equal(ReviewNotificationStatus.Failed, n.Status);
        }

        [Fact]
        public async Task Save_failing_after_send_leaves_the_row_claimed_not_pending()
        {
            // Simulates the DB: only what was successfully saved survives.
            var n = new ReviewNotification { Status = ReviewNotificationStatus.Pending };
            ReviewNotificationStatus? persisted = null;
            var saves = 0;

            await Assert.ThrowsAsync<InvalidOperationException>(() => NotificationDelivery.DeliverAsync(
                n,
                send: () => Task.CompletedTask,
                save: () =>
                {
                    saves++;
                    if (saves == 2)
                    {
                        throw new InvalidOperationException("db connection lost");
                    }

                    persisted = n.Status;
                    return Task.CompletedTask;
                },
                MaxAttempts));

            // The next run only picks up Pending rows, so this email is never sent a second time.
            Assert.Equal(ReviewNotificationStatus.Sending, persisted);
        }

        [Fact]
        public async Task Claim_save_failing_means_nothing_is_sent()
        {
            var n = new ReviewNotification { Status = ReviewNotificationStatus.Pending };
            var sendCalls = 0;

            await Assert.ThrowsAsync<InvalidOperationException>(() => NotificationDelivery.DeliverAsync(
                n,
                send: () => { sendCalls++; return Task.CompletedTask; },
                save: () => throw new InvalidOperationException("db down"),
                MaxAttempts));

            Assert.Equal(0, sendCalls);
        }
    }
}
