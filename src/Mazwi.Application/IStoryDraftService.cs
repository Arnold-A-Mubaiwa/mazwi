namespace Mazwi.Application;

public interface IStoryDraftService
{
    Task<StoryDraftDto?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<StoryDraftDto?> SaveTranscriptAsync(Guid id, string text, CancellationToken cancellationToken);
    Task<StoryDraftDto?> SaveTranslationAsync(Guid id, string language, string text, CancellationToken cancellationToken);
    Task<StoryDraftDto?> SaveTranslationWithNotesAsync(Guid id, string language, string text, IReadOnlyList<string>? notes, CancellationToken cancellationToken);
    Task<StoryDraftDto?> DraftTranslationAsync(Guid id, string language, Guid expectedVersion, CancellationToken cancellationToken);
}
