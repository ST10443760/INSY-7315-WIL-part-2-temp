using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    // Simple polling background service: runs the day-before reminder and
    // morning-of location email checks once an hour. No heavier job
    // scheduler needed at this scale.
    public class ScheduledNotificationHostedService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ScheduledNotificationHostedService> _logger;

        public ScheduledNotificationHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<ScheduledNotificationHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);

            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task RunOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                // IScheduledNotificationService is Scoped (it depends on the
                // Scoped ApplicationDbContext via INotificationService), so
                // it needs its own scope here - this hosted service itself
                // is a Singleton with no request scope to borrow.
                using var scope = _scopeFactory.CreateScope();
                var scheduledNotificationService = scope.ServiceProvider.GetRequiredService<IScheduledNotificationService>();

                await scheduledNotificationService.SendDueRemindersAsync();
                await scheduledNotificationService.SendDueLocationEmailsAsync();
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Don't let one failed run kill the background service - it
                // should keep trying on the next tick.
                _logger.LogError(ex, "Scheduled notification check failed");
            }
        }
    }
}
