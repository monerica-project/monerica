namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>
    /// Site-wide cap on the submission form's "does this link load" check: at most
    /// <see cref="MaxPerHour"/> outbound checks in any rolling hour. No visitor identifier is kept.
    /// </summary>
    public sealed class SubmissionLinkCheckThrottle
    {
        public const int MaxPerHour = 60;

        private readonly object sync = new ();
        private readonly Queue<DateTimeOffset> recent = new ();
        private readonly TimeProvider clock;

        public SubmissionLinkCheckThrottle(TimeProvider? clock = null)
        {
            this.clock = clock ?? TimeProvider.System;
        }

        /// <summary>Reserves one check; false when the hourly cap is reached.</summary>
        /// <returns>True when a check may run.</returns>
        public bool TryReserve()
        {
            var now = this.clock.GetUtcNow();
            lock (this.sync)
            {
                while (this.recent.Count > 0 && now - this.recent.Peek() >= TimeSpan.FromHours(1))
                {
                    this.recent.Dequeue();
                }

                if (this.recent.Count >= MaxPerHour)
                {
                    return false;
                }

                this.recent.Enqueue(now);
                return true;
            }
        }
    }
}
