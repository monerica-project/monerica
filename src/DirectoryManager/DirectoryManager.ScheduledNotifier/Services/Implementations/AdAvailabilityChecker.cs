using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Notifications;
using DirectoryManager.ScheduledNotifier.Services.Interfaces;
using Hangfire;
using System.Text.Json;

namespace DirectoryManager.ScheduledNotifier.Services.Implementations
{
    public class AdAvailabilityChecker : IAdAvailabilityChecker
    {
        private readonly NotificationService _notificationService;
        private readonly HttpClient _httpClient;

        public AdAvailabilityChecker(NotificationService notificationService, IHttpClientFactory httpClientFactory)
        {
            _notificationService = notificationService;
            _httpClient = httpClientFactory.CreateClient();
        }

        // This is the method Hangfire will call
        public void ScheduleCheckAndSendNotifications()
        {
            // Use BackgroundJob to run the async method in the background
            BackgroundJob.Enqueue(() => CheckAndSendNotifications());
        }

        // The actual async method that handles notifications
        public async Task CheckAndSendNotifications()
        {
            try
            {
                var notifications = await FetchNotificationsFromApi();

                foreach (var notification in notifications)
                {
                    if (notification.NotificationType == NotificationType.ExpirationReminder &&
                        notification.ExpirationDate.HasValue &&
                        notification.ExpirationDate.Value - DateTime.UtcNow <= TimeSpan.FromHours(24))
                    {
                        await _notificationService.SendEmailAsync(
                            notification.Email,
                            "Reminder: Expiration Approaching",
                            $"Your subscription will expire on {notification.ExpirationDate.Value}");
                    }
                    else if (notification.NotificationType == NotificationType.AdSpotAvailability)
                    {
                        await _notificationService.SendEmailAsync(
                            notification.Email,
                            "Ad Spot Available",
                            "An ad spot is now available!");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking notifications: {ex.Message}");
            }
        }

        // Helper method to fetch notifications from the API
        private async Task<List<Notification>> FetchNotificationsFromApi()
        {
            var response = await _httpClient.GetAsync("https://your-website.com/api/notifications");

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to fetch notifications from the API.");
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<Notification>>(content) ?? new List<Notification>();
        }
    }
}