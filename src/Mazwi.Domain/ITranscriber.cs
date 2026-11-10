namespace Mazwi.Domain;

public interface ITranscriber
{
    Task<StoryText> TranscribeAsync(Stream content, StoryRecording recording, CancellationToken cancellationToken);
}

public sealed class TranscriptionUnavailableException(bool timedOut = false) : Exception("AI transcription is unavailable. Your saved transcript has not changed.")
{
    public bool TimedOut { get; } = timedOut;
}
