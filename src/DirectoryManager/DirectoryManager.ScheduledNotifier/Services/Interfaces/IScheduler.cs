using DirectoryManager.Data.Models.Notifications;

namespace DirectoryManager.ScheduledNotifier.Services.Interfaces
{
    public interface IScheduler
    {
        /// <summary>
        /// Starts the scheduler and sets up recurring jobs.
        /// </summary>
        void Start();

        /// <summary>
        /// Fetches notifications asynchronously.
        /// </summary>
        Task<List<Notification>> GetNotificationsAsync();

        /// <summary>
        /// Fetches ad spots asynchronously.
        /// </summary>
        Task<List<AdSpot>> GetAdSpotsAsync();
    }
}