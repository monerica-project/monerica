using System.Net.Mail;
using DirectoryManager.Data.Models.Notifications;
using DirectoryManager.Data.Enums;
using DirectoryManager.ScheduledNotifier.Services.Interfaces;

namespace DirectoryManager.ScheduledNotifier.Services.Implementations
{
    public class NotificationService : INotificationService
    {
        public async Task SendEmailAsync(string email, string subject, string body)
        {
            using (var client = new SmtpClient("smtp.your-email-provider.com"))
            {
                var message = new MailMessage("notifications@yourdomain.com", email)
                {
                    Subject = subject,
                    Body = body
                };
                await client.SendMailAsync(message);
            }
        }

        public async Task SendExpirationReminder(Notification notification, TimeSpan reminderBefore)
        {
            if (notification.ExpirationDate.HasValue &&
                notification.ExpirationDate.Value - DateTime.UtcNow <= reminderBefore &&
                !notification.IsSent)
            {
                string subject = "Reminder: Expiration Approaching";
                string body = $"This is a reminder that an item will expire on {notification.ExpirationDate.Value}.";
                await SendEmailAsync(notification.Email, subject, body);

                notification.IsSent = true; // Mark as sent
            }
        }

        public async Task SendAdAvailabilityNotification(Notification notification, AdSpot adSpot)
        {
            if (adSpot.IsAvailable && !notification.IsSent)
            {
                string subject = "Ad Spot Available";
                string body = $"An ad spot '{adSpot.Name}' is now available.";
                await SendEmailAsync(notification.Email, subject, body);

                notification.IsSent = true; // Mark as sent
            }
        }

        public async Task SendSponsorNotification(Notification notification, SponsorshipType sponsorType)
        {
            string subject = $"{sponsorType} Ad Spot Available";
            string body = $"A {sponsorType} ad spot is now available.";

            await SendEmailAsync(notification.Email, subject, body);
            notification.IsSent = true; // Mark as sent
        }
    }
}