namespace DirectoryManager.ScheduledNotifier.Services.Interfaces
{
    public interface IAdAvailabilityChecker
    {
        /// <summary>
        /// Schedules the notification check to run in the background.
        /// </summary>
        void ScheduleCheckAndSendNotifications();

        /// <summary>
        /// Checks ad availability and sends notifications if required.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CheckAndSendNotifications();
    }
}