namespace DirectoryManager.Data.Enums
{
    // Stored as int in DB by default.
    public enum ReviewTagLevel : byte
    {
        // neutral/unknown
        Neutral = 0,

        // positive trust signals
        Verified = 1,      // green

        // informational / “heads up”
        Info = 2,          // blue
        Warning = 3,       // yellow/orange

        // negative / risky
        Risk = 4,          // orange
        KycIssue = 5,      // red
        Scam = 6           // dark red
    }
}