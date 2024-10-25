using DirectoryManager.Data.Models.Notifications;
using DirectoryManager.ScheduledNotifier.Services;
using Hangfire;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DirectoryManager.ScheduledNotifier.Services
{

    public class Scheduler
    {
        private readonly AdAvailabilityChecker _checker;

        public Scheduler(AdAvailabilityChecker checker)
        {
            _checker = checker;
        }

        public void Start()
        {
            //// Schedule job to run every hour
            //RecurringJob.AddOrUpdate("check-ad-availability", async () =>
            //{
            //    var notifications = await GetNotificationsAsync(); // Fetch from database
            //    var adSpots = await GetAdSpotsAsync(); // Fetch from database

            //    await _checker.CheckAndSendExpirationReminders(notifications);
            //    await _checker.CheckAndNotifyAdSpots(notifications, adSpots);
            //}, Cron.Hourly);
        }

        private Task<List<Notification>> GetNotificationsAsync()
        {
            // Simulate fetching notifications from the database
            return Task.FromResult(new List<Notification>());
        }

        private Task<List<AdSpot>> GetAdSpotsAsync()
        {
            // Simulate fetching ad spots from the database
            return Task.FromResult(new List<AdSpot>());
        }
    }
}