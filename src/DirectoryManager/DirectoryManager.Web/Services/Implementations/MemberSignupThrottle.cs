namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>
    /// Site-wide brake on new member accounts: at most <see cref="MaxPerHour"/> sign-ups in any
    /// rolling hour, whoever sends them. No visitor IP or other identifier is looked at or kept,
    /// only the times of recent sign-ups (bounded to <see cref="MaxPerHour"/> entries).
    /// The captcha is the per-visitor brake; this only caps a captcha-solving flood.
    /// </summary>
    public sealed class MemberSignupThrottle
    {
        public const int MaxPerHour = 60;

        private readonly object sync = new ();
        private readonly Queue<DateTimeOffset> recent = new ();
        private readonly TimeProvider clock;

        public MemberSignupThrottle(TimeProvider? clock = null)
        {
            this.clock = clock ?? TimeProvider.System;
        }

        /// <summary>Reserves one sign-up slot; false when the hourly cap is reached.</summary>
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
