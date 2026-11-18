namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>
    /// Site-wide brake on new submission drafts: at most <see cref="MaxPerHour"/> new drafts in
    /// any rolling hour, whoever sends them. A draft is saved before the captcha (so a wrong
    /// captcha answer never loses the form), which let a script create about 400 of them on
    /// 2026-11-16. No visitor IP or other identifier is looked at or kept, only the times of
    /// recent drafts. Normal traffic is around ten submissions a day.
    /// </summary>
    public sealed class SubmissionDraftThrottle
    {
        public const int MaxPerHour = 30;

        /// <summary>Extra drafts per hour that are accepted once the visitor solves a captcha.</summary>
        public const int MaxWithCaptchaPerHour = 120;

        public const string BusyMessage =
            "A lot of submissions are coming in right now. Your form is kept below: type the captcha code shown above the button and press it again.";

        private readonly Queue<DateTimeOffset> recentWithCaptcha = new ();

        private readonly object sync = new ();
        private readonly Queue<DateTimeOffset> recent = new ();
        private readonly TimeProvider clock;

        public SubmissionDraftThrottle(TimeProvider? clock = null)
        {
            this.clock = clock ?? TimeProvider.System;
        }

        /// <summary>Reserves a slot from the captcha-gated overflow allowance.</summary>
        /// <returns>True when the draft may be created.</returns>
        public bool TryReserveWithCaptcha()
        {
            var now = this.clock.GetUtcNow();
            lock (this.sync)
            {
                while (this.recentWithCaptcha.Count > 0 && now - this.recentWithCaptcha.Peek() >= TimeSpan.FromHours(1))
                {
                    this.recentWithCaptcha.Dequeue();
                }

                if (this.recentWithCaptcha.Count >= MaxWithCaptchaPerHour)
                {
                    return false;
                }

                this.recentWithCaptcha.Enqueue(now);
                return true;
            }
        }

        /// <summary>Reserves one new-draft slot; false when the hourly cap is reached.</summary>
        /// <returns>True when a draft may be created.</returns>
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
