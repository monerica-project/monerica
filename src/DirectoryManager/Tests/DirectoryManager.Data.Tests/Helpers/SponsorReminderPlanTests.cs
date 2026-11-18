using DirectoryManager.Data.Helpers;

namespace DirectoryManager.Data.Tests.Helpers
{
    public class SponsorReminderPlanTests
    {
        private static readonly DateTime Now = new (2026, 11, 15, 18, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void MoreThan72HoursLeft_NothingIsDue()
        {
            Assert.Equal(SponsorReminderKind.None, SponsorReminderPlan.Due(Now, Now.AddHours(73), false, null, null));
        }

        [Fact]
        public void Within72Hours_FirstReminder_Once()
        {
            Assert.Equal(SponsorReminderKind.First, SponsorReminderPlan.Due(Now, Now.AddHours(70), false, null, null));
            Assert.Equal(SponsorReminderKind.None, SponsorReminderPlan.Due(Now, Now.AddHours(60), false, Now.AddHours(-10), null));
        }

        [Fact]
        public void OldInvoiceWithOnlyTheLegacyFlag_CountsAsFirstSent()
        {
            Assert.Equal(SponsorReminderKind.None, SponsorReminderPlan.Due(Now, Now.AddHours(40), true, null, null));
            Assert.Equal(SponsorReminderKind.Final, SponsorReminderPlan.Due(Now, Now.AddHours(22.6), true, null, null));
        }

        [Fact]
        public void Within24Hours_FinalReminder_Once()
        {
            Assert.Equal(SponsorReminderKind.Final, SponsorReminderPlan.Due(Now, Now.AddHours(23), true, Now.AddHours(-48), null));
            Assert.Equal(SponsorReminderKind.None, SponsorReminderPlan.Due(Now, Now.AddHours(20), true, Now.AddHours(-51), Now.AddHours(-3)));
            Assert.Equal(SponsorReminderKind.None, SponsorReminderPlan.Due(Now, Now.AddHours(1), true, Now.AddHours(-70), Now.AddHours(-22)));
        }

        [Fact]
        public void ShortSponsorship_GetsOnlyTheFinalNotice_NeverBoth()
        {
            // Bought with 10 hours left and nothing sent yet: the final notice, not the first.
            Assert.Equal(SponsorReminderKind.Final, SponsorReminderPlan.Due(Now, Now.AddHours(10), false, null, null));

            // After the final notice the first one can never fire for this period.
            Assert.Equal(SponsorReminderKind.None, SponsorReminderPlan.Due(Now, Now.AddHours(7), false, null, Now.AddHours(-3)));
        }

        [Fact]
        public void FinalWaitsWhenTheFirstJustWentOut()
        {
            Assert.Equal(SponsorReminderKind.None, SponsorReminderPlan.Due(Now, Now.AddHours(23), true, Now.AddHours(-3), null));
            Assert.Equal(SponsorReminderKind.Final, SponsorReminderPlan.Due(Now, Now.AddHours(11), true, Now.AddHours(-15), null));
        }

        [Fact]
        public void EndedSponsorship_NothingIsDue()
        {
            Assert.Equal(SponsorReminderKind.None, SponsorReminderPlan.Due(Now, Now.AddMinutes(-1), false, null, null));
        }

        [Theory]
        [InlineData(22.65, 23)]
        [InlineData(23.9, 24)]
        [InlineData(0.2, 1)]
        public void HoursLeft_IsRounded(double hours, int expected)
        {
            Assert.Equal(expected, SponsorReminderPlan.HoursLeft(Now, Now.AddHours(hours)));
        }
    }
}
