namespace Mazwi.Domain;

public interface ISpeechGenerator
{
    Task<GeneratedSpeech> GenerateAsync(SpeechScript script, string language, string storyType, CancellationToken cancellationToken);
}

public sealed class GeneratedSpeech
{
    private readonly byte[] _wav;

    public GeneratedSpeech(byte[] wav)
    {
        ArgumentNullException.ThrowIfNull(wav);
        if (wav.Length <= SpeechScript.WavHeaderBytes || wav.Length > SpeechScript.WavHeaderBytes + SpeechScript.MaximumAudioBytes)
        {
            throw new DomainException("Generated speech must be non-empty and no longer than two minutes.");
        }

        _wav = wav.ToArray();
    }

    public int Length => _wav.Length;

    public Stream OpenRead() => new MemoryStream(_wav, writable: false);
}

public sealed class SpeechUnavailableException(bool timedOut = false) : Exception("AI voice is unavailable. Your saved draft has not changed.")
{
    public bool TimedOut { get; } = timedOut;
}
