namespace Mazwi.Domain;

public interface IStoryRepository
{
    Task AddAsync(Story story, CancellationToken cancellationToken);
    Task<Story?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task SaveTranscriptAsync(Story story, CancellationToken cancellationToken);
    Task SaveTranslationAsync(Guid storyId, TranslationDraft draft, CancellationToken cancellationToken);
}
