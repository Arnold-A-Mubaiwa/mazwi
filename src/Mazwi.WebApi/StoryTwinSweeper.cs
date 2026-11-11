using Mazwi.Application;

namespace Mazwi.WebApi;

/// <summary>
/// Regularly fingerprints newly published or changed stories and suggests their twins. It keeps no state of its own:
/// the database says what still needs doing, so nothing is lost on restart.
/// </summary>
public sealed class StoryTwinSweeper(IServiceScopeFactory scopes, TwinSettings settings, ILogger<StoryTwinSweeper> logger) : BackgroundService
{
    public const int BatchSize = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.CanRun)
        {
            return;
        }

        using var timer = new PeriodicTimer(settings.SweepInterval);
        do
        {
            try
            {
                await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IStoryTwinService>().RefreshAsync(BatchSize, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Log the type only: messages could contain story text.
                logger.LogWarning("Story Twins sweep failed ({ExceptionType}).", exception.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
