namespace Mazwi.Application;

public interface IStoryService
{
    Task<StoryDto> UploadAsync(UploadStoryCommand command, Stream content, CancellationToken cancellationToken);
    Task<StoryDto?> GetAsync(Guid id, CancellationToken cancellationToken);
}
