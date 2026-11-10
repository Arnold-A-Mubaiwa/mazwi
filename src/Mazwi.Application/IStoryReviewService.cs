namespace Mazwi.Application;

public interface IStoryReviewService
{
    Task<StoryDraftDto?> ApproveAsync(Guid id, string language, Guid reviewVersion, bool speakerChecked, CancellationToken cancellationToken);
    Task<StoryDraftDto?> UploadRecordingAsync(UploadTranslationRecordingCommand command, Stream content, CancellationToken cancellationToken);
    Task<RecordingPlaybackDto?> GetRecordingAsync(Guid id, string language, CancellationToken cancellationToken);
}
