namespace Mazwi.Application;

public interface IDemoSeedService
{
    /// <summary>
    /// Creates the demo artists (email confirmed) and publishes the demo stories with their words.
    /// Does nothing when the first demo artist already exists, so it is safe to run on every start.
    /// </summary>
    Task<DemoSeedResult> SeedAsync(DemoContent content, Func<DemoStory, Stream> openRecording, CancellationToken cancellationToken);
}
