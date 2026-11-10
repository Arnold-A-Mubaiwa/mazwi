namespace Mazwi.Application;

public interface IStoryDraftService
{
    Task<StoryDraftDto?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<StoryDraftDto?> SaveTranscriptAsync(Guid id, string text, CancellationToken cancellationToken);
    Task<StoryDraftDto?> SaveTranslationAsync(Guid id, string language, string text, CancellationToken cancellationToken);
}
