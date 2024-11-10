using DirectoryManager.Data.Models.Notifications;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.ScheduledNotifier.Services.Interfaces
{
    public interface INotificationService
    {
        /// <summary>
        /// Sends an email asynchronously.
        /// </summary>
        /// <param name="email">Recipient's email address.</param>
        /// <param name="subject">Subject of the email.</param>
        /// <param name="body">Body content of the email.</param>
        Task SendEmailAsync(string email, string subject, string body);

        /// <summary>
        /// Sends an expiration reminder if the expiration date is approaching.
        /// </summary>
        /// <param name="notification">Notification details.</param>
        /// <param name="reminderBefore">Time span to send the reminder before expiration.</param>
        Task SendExpirationReminder(Notification notification, TimeSpan reminderBefore);

        /// <summary>
        /// Sends an ad availability notification if an ad spot becomes available.
        /// </summary>
        /// <param name="notification">Notification details.</param>
        /// <param name="adSpot">Ad spot information.</param>
        Task SendAdAvailabilityNotification(Notification notification, AdSpot adSpot);

        /// <summary>
        /// Sends a sponsor notification based on the type of sponsorship.
        /// </summary>
        /// <param name="notification">Notification details.</param>
        /// <param name="sponsorType">Type of sponsorship ad spot available.</param>
        Task SendSponsorNotification(Notification notification, SponsorshipType sponsorType);
    }
}