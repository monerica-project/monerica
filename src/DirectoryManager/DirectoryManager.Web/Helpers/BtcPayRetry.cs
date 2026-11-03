namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Retries a BTCPay call a few times before giving up. A single failed DNS lookup or a dropped
    /// connection used to show "Donations aren't available right now" (2026-11-03: the app server's
    /// resolver failed ~1 in 4 lookups of the BTCPay host). Each failure is written to the service
    /// log so the real cause is visible (the pay flows used to swallow it).
    /// </summary>
    public static class BtcPayRetry
    {
        private static readonly TimeSpan[] Delays = { TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(1200) };

        public static async Task<T> RunAsync<T>(Func<Task<T>> call, string what)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return await call().ConfigureAwait(false);
                }
                catch (Exception ex) when (attempt < Delays.Length && ex is not OperationCanceledException)
                {
                    Console.Error.WriteLine($"[btcpay] {what} failed (attempt {attempt + 1}), retrying: {ex.GetType().Name}: {ex.Message}");
                    await Task.Delay(Delays[attempt]).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[btcpay] {what} failed after {attempt + 1} attempts: {ex.GetType().Name}: {ex.Message}");
                    throw;
                }
            }
        }
    }
}
