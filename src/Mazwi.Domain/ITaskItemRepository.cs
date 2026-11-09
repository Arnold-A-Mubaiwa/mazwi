namespace Mazwi.Domain;

public interface ITaskItemRepository
{
    Task AddAsync(TaskItem taskItem, CancellationToken cancellationToken);
    Task<TaskItem?> GetAsync(Guid id, CancellationToken cancellationToken);
}
