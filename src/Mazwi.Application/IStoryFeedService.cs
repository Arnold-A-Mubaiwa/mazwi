namespace Mazwi.Application;

public interface IStoryFeedService
{
    Task<StoryFeedPageDto> GetPageAsync(string language, int offset, int limit, CancellationToken cancellationToken);
    Task<StoryViewDto?> GetAsync(Guid id, string language, CancellationToken cancellationToken);
    Task<RecordingPlaybackDto?> GetRecordingAsync(Guid id, string language, Guid reviewVersion, bool originalAudio, CancellationToken cancellationToken);
    // The subtitle cues for exactly the version the recording route plays, or null when there are none.
    Task<IReadOnlyList<SubtitleCueDto>?> GetSubtitlesAsync(Guid id, string language, Guid reviewVersion, bool originalAudio, CancellationToken cancellationToken);
}
