namespace DirectoryManager.Data.Models
{
    /// <summary>AI calls on one UTC day.</summary>
    public sealed record AiCallDailyTotal(DateTime Day, int Calls, int Errors, long InputTokens, long OutputTokens);
}
