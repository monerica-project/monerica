using DirectoryManager.BackgroundServices.Interfaces;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Services.Interfaces;

namespace DirectoryManager.BackgroundServices
{
    public class EmailSubscriptionMonitor : IEmailSubscriptionMonitor
    {
        private readonly IEmailSubscriptionRepository _repository;
        private readonly IEmailService _emailService;
        private bool _isRunning;

        public EmailSubscriptionMonitor(IEmailSubscriptionRepository repository, IEmailService emailService)
        {
            _repository = repository;
            _emailService = emailService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _isRunning = true;
            var lastProcessedId = 0;

            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                var newSubscriptions = _repository
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
            _isRunning = false;
        }
    }
}