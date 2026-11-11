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
    Task SaveSubtitleTimingAsync(Story story, string language, Guid expectedVersion, CancellationToken cancellationToken);
    Task SaveUnpublishAsync(Story story, Guid expectedVersion, CancellationToken cancellationToken);
    // Deletes the story and everything stored with it in the database. False when it does not exist.
    Task<bool> DeleteAsync(Guid id, Guid expectedVersion, CancellationToken cancellationToken);
    Task SaveHiddenForReviewAsync(Story story, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> ListIdsAsync(bool approvedOnly, int offset, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> ListOwnedIdsAsync(Guid ownerId, int offset, int limit, CancellationToken cancellationToken);
    // Gives every story saved before accounts existed to one artist. Returns how many were claimed.
    Task<int> ClaimUnownedAsync(Guid ownerId, CancellationToken cancellationToken);
}
