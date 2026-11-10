namespace Mazwi.Domain;

public interface ITranslationDrafter
{
    Task<TranslationDraftResult> DraftAsync(string text, string source, string target, string storyType, CancellationToken cancellationToken);
}

public sealed record TranslationDraftResult(StoryText Translation, TranslationNotes Notes);

public sealed class TranslationDraftUnavailableException(bool timedOut = false) : Exception("AI translation is unavailable. Your saved draft has not changed.")
{
    public bool TimedOut { get; } = timedOut;
}

public sealed class AiDraftNotAllowedException(string message) : Exception(message);
