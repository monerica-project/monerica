using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Double-POST guard for the no-JS / no-cookie compose flows (reviews, replies), keyed by flowId.
    ///
    /// Only one POST per flow runs the submit at a time. A duplicate POST (double click) that
    /// arrives while the first is still in flight WAITS for it (up to <see cref="DuplicateWait"/>)
    /// instead of guessing:
    ///   - first one saved      -> the duplicate gets the "already saved" result (Thanks page);
    ///   - first one didn't save (validation error etc.) -> the claim was released, so the duplicate
    ///     claims the flow and runs normally (it carries the same posted form, so the user sees the
    ///     same validation errors with their text intact);
    ///   - first one still running after the wait -> the "still in flight" result (back to compose).
    ///
    /// A saved flow keeps its completed claim as a tombstone, so later replays of the same flowId
    /// short-circuit to the "already saved" result.
    /// </summary>
    public sealed class FlowSubmitGuard
    {
        // Each failed attempt releases the claim; cap how many times one request re-tries to claim.
        private const int MaxClaimAttempts = 3;

        public static readonly TimeSpan DuplicateWait = TimeSpan.FromSeconds(15);

        // Value = the claim of the POST currently running (or the saved tombstone).
        // Result true = saved, false = finished without saving (claim already removed).
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<bool>> flows = new ();

        /// <summary>
        /// Runs <paramref name="submit"/> holding the flow's claim. The submit must call the
        /// supplied "mark saved" action as soon as its content is persisted.
        /// </summary>
        /// <returns>The submit's result, or one of the duplicate outcomes.</returns>
        public async Task<IActionResult> RunAsync(
            Guid flowId,
            Func<Action, Task<IActionResult>> submit,
            Func<IActionResult> onAlreadySaved,
            Func<IActionResult> onStillInFlight,
            CancellationToken ct)
        {
            for (var attempt = 1; ; attempt++)
            {
                var claim = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var current = this.flows.GetOrAdd(flowId, claim);

                if (ReferenceEquals(current, claim))
                {
                    return await this.RunClaimedAsync(flowId, claim, submit);
                }

                // Another POST for this flow holds the claim (or already saved). Wait for its outcome.
                bool otherSaved;
                try
                {
                    otherSaved = await current.Task.WaitAsync(DuplicateWait, ct);
                }
                catch (TimeoutException)
                {
                    return onStillInFlight();
                }

                if (otherSaved)
                {
                    return onAlreadySaved();
                }

                if (attempt >= MaxClaimAttempts)
                {
                    return onStillInFlight();
                }

                // The other POST finished WITHOUT saving and released its claim: loop and
                // process this POST normally.
            }
        }

        /// <summary>Drops any claim/tombstone for a flow (e.g. when the flow itself is discarded).</summary>
        public void Forget(Guid flowId) => this.flows.TryRemove(flowId, out _);

        private async Task<IActionResult> RunClaimedAsync(
            Guid flowId,
            TaskCompletionSource<bool> claim,
            Func<Action, Task<IActionResult>> submit)
        {
            // Released on EVERY path that doesn't save (validation errors, exceptions, new early
            // returns) so a corrected resubmit is never silently dropped; a saved flow keeps the
            // claim as its replay tombstone.
            var saved = false;
            try
            {
                return await submit(() => saved = true);
            }
            finally
            {
                if (!saved)
                {
                    // Only remove OUR claim (never someone else's).
                    this.flows.TryRemove(new KeyValuePair<Guid, TaskCompletionSource<bool>>(flowId, claim));
                }

                // Wake any duplicate POSTs waiting on this one (after the claim is released, so a
                // waiter that re-tries can claim the flow).
                claim.TrySetResult(saved);
            }
        }
    }
}
