using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class TaskItemService(ITaskItemRepository repository) : ITaskItemService
{
    public async Task<TaskItemDto> CreateAsync(string title, CancellationToken cancellationToken)
    {
        TaskItem taskItem = TaskItem.Create(title);
        await repository.AddAsync(taskItem, cancellationToken);
        return new TaskItemDto(taskItem.Id, taskItem.Title.Value);
    }

    public async Task<TaskItemDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        TaskItem? taskItem = await repository.GetAsync(id, cancellationToken);
        return taskItem is null ? null : new TaskItemDto(taskItem.Id, taskItem.Title.Value);
    }
}
