namespace SessionManagement.Server.Services
{
    public class AutoTerminationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan         _interval = TimeSpan.FromSeconds(60);

        public AutoTerminationBackgroundService(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            Console.WriteLine(
                "[AutoTerminate] Background service started. " +
                "Checking every 60 seconds.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope =
                        _serviceProvider.CreateScope();

                    var terminationService =
                        scope.ServiceProvider
                             .GetRequiredService<TerminationService>();

                    await terminationService
                        .AutoTerminateExpiredSessionsAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"[AutoTerminate] Background error: {ex.Message}");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
    }
}
