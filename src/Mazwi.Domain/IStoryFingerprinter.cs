namespace Mazwi.Domain;

public interface IStoryFingerprinter
{
    Task<StoryFingerprint> FingerprintAsync(string text, string language, string storyType, CancellationToken cancellationToken);
}

public sealed class StoryFingerprintUnavailableException() : Exception("The story fingerprint could not be made.");
