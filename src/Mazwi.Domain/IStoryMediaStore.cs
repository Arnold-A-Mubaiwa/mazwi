namespace Mazwi.Domain;

public interface IStoryMediaStore
{
    Task SaveAsync(Guid id, StoryRecording recording, Stream content, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, StoryRecording recording, CancellationToken cancellationToken);
}
