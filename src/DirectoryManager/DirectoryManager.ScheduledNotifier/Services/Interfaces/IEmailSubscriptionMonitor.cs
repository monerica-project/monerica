namespace DirectoryManager.BackgroundServices.Interfaces
{
    public interface IEmailSubscriptionMonitor
    {
        /// <summary>
        /// Starts monitoring for new email subscriptions and sends welcome emails.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the monitoring process.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task StartAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Stops the monitoring process.
        /// </summary>
        void Stop();
    }
}