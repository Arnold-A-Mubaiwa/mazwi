namespace Mazwi.Application;

public interface ITaskItemService
{
    Task<TaskItemDto> CreateAsync(string title, CancellationToken cancellationToken);
    Task<TaskItemDto?> GetAsync(Guid id, CancellationToken cancellationToken);
}
