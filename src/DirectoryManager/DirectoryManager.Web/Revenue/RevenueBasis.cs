namespace DirectoryManager.Web.Revenue
{
    // How revenue is dated: CASH counts money on the day it was paid; EARNED spreads each
    // sponsorship over the days it pays for (everything else is earned when paid).
    public enum RevenueBasis
    {
        Earned = 0,
        Cash = 1,
    }
}
