using DirectoryManager.Data.Enums;

namespace DirectoryManager.ReviewOrderChecker.Check
{
    /// <summary>Facts about the order link that were worked out in code before the AI was asked.</summary>
    public sealed record LinkFacts(bool OnListingSite, string OrderSite, int? ReusedOnReviewId, bool ReusedByOtherAuthor);

    public sealed record OrderDecision(AiOrderCheckResult Result, string Summary, IReadOnlyList<string> Notes);

    /// <summary>
    /// Turns the AI's reading plus the code-level facts into the stored result. The AI alone can
    /// never produce "checks out": the link must be on the listing's own site, must not be another
    /// reviewer's order, and the order must be finished.
    /// </summary>
    public static class OrderCheckDecision
    {
        public const decimal MinConfidence = 0.7m;

        public static OrderDecision Decide(OrderVerdict v, LinkFacts facts)
        {
            var notes = new List<string>();
            var result = v.Result switch
            {
                OrderVerdict.ChecksOut => AiOrderCheckResult.ChecksOut,
                OrderVerdict.DoesNotCheckOut => AiOrderCheckResult.DoesNotCheckOut,
                OrderVerdict.InProgress => AiOrderCheckResult.InProgress,
                _ => AiOrderCheckResult.CouldNotVerify,
            };

            if (facts.ReusedByOtherAuthor && facts.ReusedOnReviewId is { } other)
            {
                notes.Add($"The same order link was already used by a different reviewer on review #{other}.");
                result = AiOrderCheckResult.DoesNotCheckOut;
            }
            else if (facts.ReusedOnReviewId is { } same)
            {
                notes.Add($"The same reviewer used this order link before on review #{same}.");
            }

            if (result == AiOrderCheckResult.ChecksOut)
            {
                if (!facts.OnListingSite)
                {
                    notes.Add($"The order link is on {facts.OrderSite}, which is not one of this listing's addresses, so it cannot count as checked. If that is the listing's own domain, add it to the listing.");
                    result = AiOrderCheckResult.CouldNotVerify;
                }
                else if (v.OrderStatus != OrderProofStatus.Completed || !v.OrderVisible)
                {
                    notes.Add("The page does not show a finished order.");
                    result = AiOrderCheckResult.CouldNotVerify;
                }
                else if (v.Confidence < MinConfidence)
                {
                    notes.Add($"The AI was not sure enough ({v.Confidence:0.00}).");
                    result = AiOrderCheckResult.CouldNotVerify;
                }
                else if (v.ReviewFitsOrder == "no")
                {
                    notes.Add("The review's own details do not match the order.");
                    result = AiOrderCheckResult.CouldNotVerify;
                }
            }
            else if (!facts.OnListingSite && result != AiOrderCheckResult.DoesNotCheckOut)
            {
                notes.Add($"The order link is on {facts.OrderSite}, which is not one of this listing's addresses.");
            }

            return new OrderDecision(result, v.Summary, notes);
        }
    }
}
