namespace Mazwi.Domain;

public interface IStoryRepository
{
    Task AddAsync(Story story, CancellationToken cancellationToken);
    Task<Story?> GetAsync(Guid id, CancellationToken cancellationToken);
}
