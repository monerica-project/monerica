namespace DirectoryManager.Web.Tests.Security
{
    /// <summary>A settable clock (no extra test package needed).</summary>
    public sealed class MutableClock : TimeProvider
    {
        private DateTimeOffset now;

        public MutableClock(DateTimeOffset now)
        {
            this.now = now;
        }

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now = this.now.Add(by);
    }
}
