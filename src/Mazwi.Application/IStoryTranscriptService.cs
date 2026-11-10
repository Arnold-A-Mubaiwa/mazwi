namespace Mazwi.Application;

public interface IStoryTranscriptService
{
    Task<StoryDraftDto?> DraftTranscriptAsync(Guid id, Guid reviewVersion, CancellationToken cancellationToken);
}
