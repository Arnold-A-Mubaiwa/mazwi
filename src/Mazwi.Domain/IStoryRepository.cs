namespace Mazwi.Domain;

public interface IStoryRepository
{
    Task AddAsync(Story story, CancellationToken cancellationToken);
    Task<Story?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task SaveTranscriptAsync(Story story, CancellationToken cancellationToken);
    Task SaveAiTranscriptAsync(Story story, Guid expectedVersion, CancellationToken cancellationToken);
    Task SaveTranslationAsync(Guid storyId, TranslationDraft draft, CancellationToken cancellationToken);
    Task SaveAiTranslationAsync(Guid storyId, TranslationDraft draft, Guid expectedVersion, CancellationToken cancellationToken);
    Task SaveApprovalAsync(Guid storyId, string language, Guid expectedVersion, bool speakerChecked, CancellationToken cancellationToken);
    Task SaveRecordingAsync(Guid storyId, TranslationDraft draft, Guid expectedVersion, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> ListIdsAsync(bool approvedOnly, int offset, int limit, CancellationToken cancellationToken);
}
