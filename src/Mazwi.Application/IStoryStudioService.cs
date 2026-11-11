namespace Mazwi.Application;

public interface IStoryStudioService
{
    Task<StoryStudioPageDto> GetPageAsync(Guid ownerId, int offset, int limit, CancellationToken cancellationToken);
}
