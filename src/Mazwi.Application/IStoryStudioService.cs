namespace Mazwi.Application;

public interface IStoryStudioService
{
    Task<StoryStudioPageDto> GetPageAsync(int offset, int limit, CancellationToken cancellationToken);
}
