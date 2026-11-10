using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class StoryStudioService(IStoryRepository repository) : IStoryStudioService
{
    public async Task<StoryStudioPageDto> GetPageAsync(int offset, int limit, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> ids = await repository.ListIdsAsync(false, offset, limit, cancellationToken);
        var items = new List<StoryDto>();
        foreach (Guid id in ids)
        {
            Story? story = await repository.GetAsync(id, cancellationToken);
            if (story is not null)
            {
                items.Add(StoryDto.From(story));
            }
        }
        return new(items, ids.Count == limit ? offset + limit : null);
    }
}
