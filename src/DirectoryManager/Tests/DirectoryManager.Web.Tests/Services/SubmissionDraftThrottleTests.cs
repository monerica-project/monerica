using DirectoryManager.Web.Services.Implementations;

namespace DirectoryManager.Web.Tests.Services
{
    public class SubmissionDraftThrottleTests
    {
        [Fact]
        public void Stops_at_the_hourly_cap_and_recovers_after_an_hour()
        {
            var clock = new SteppedClock(new DateTimeOffset(2026, 11, 16, 5, 0, 0, TimeSpan.Zero));
            var throttle = new SubmissionDraftThrottle(clock);

            for (var i = 0; i < SubmissionDraftThrottle.MaxPerHour; i++)
            {
                Assert.True(throttle.TryReserve());
            }

            Assert.False(throttle.TryReserve());

            clock.Now += TimeSpan.FromMinutes(59);
            Assert.False(throttle.TryReserve());

            clock.Now += TimeSpan.FromMinutes(2);
            Assert.True(throttle.TryReserve());
        }

        private sealed class SteppedClock : TimeProvider
        {
            public SteppedClock(DateTimeOffset start)
            {
                this.Now = start;
            }

            public DateTimeOffset Now { get; set; }

            public override DateTimeOffset GetUtcNow() => this.Now;
        }
    }
}
