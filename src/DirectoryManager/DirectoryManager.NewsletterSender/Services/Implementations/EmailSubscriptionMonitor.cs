using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.NewsletterSender.Interfaces;
using DirectoryManager.Services.Interfaces;

namespace DirectoryManager.NewsletterSender.Implementations
{
    public class EmailSubscriptionMonitor : IEmailSubscriptionMonitor
    {
        private readonly IEmailSubscriptionRepository repository;
        private readonly IEmailService emailService;
        private bool isRunning;

        public EmailSubscriptionMonitor(IEmailSubscriptionRepository repository, IEmailService emailService)
        {
            this.repository = repository;
            this.emailService = emailService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            this.isRunning = true;
            var lastProcessedId = 0;

            while (!cancellationToken.IsCancellationRequested && this.isRunning)
            {
                var newSubscriptions = this.repository
                    .GetAll()
                    .Where(x => x.EmailSubscriptionId > lastProcessedId)
                    .OrderBy(x => x.EmailSubscriptionId)
                    .ToList();

                foreach (var subscription in newSubscriptions)
                {
                   // await _emailService.SendWelcomeEmailAsync(subscription.Email);
                    lastProcessedId = subscription.EmailSubscriptionId;
                }

                // Wait before checking again
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }

        public void Stop()
        {
            this.isRunning = false;
        }
    }
}