namespace Mazwi.Domain;

public sealed record StoryRecording
{
    public const long MaximumBytes = 25_000_000;
    public string Extension { get; }
    public long Bytes { get; }

    public StoryRecording(string fileName, long bytes)
    {
        Extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (Extension is not (".mp3" or ".wav" or ".m4a" or ".mp4" or ".webm"))
        {
            throw new DomainException("Choose an MP3, WAV, M4A, MP4, or WebM recording.");
        }

        if (bytes is <= 0 or > MaximumBytes)
        {
            throw new DomainException("The recording must be non-empty and no larger than 25 MB.");
        }

        Bytes = bytes;
    }
}
