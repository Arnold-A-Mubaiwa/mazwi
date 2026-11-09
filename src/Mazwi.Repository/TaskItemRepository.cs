using Dapper;
using Mazwi.Domain;
using Npgsql;

namespace Mazwi.Repository;

public sealed class TaskItemRepository(NpgsqlDataSource dataSource) : ITaskItemRepository
{
    public async Task AddAsync(TaskItem taskItem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(taskItem);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            "INSERT INTO task_items (id, title) VALUES (@Id, @Title)",
            new { taskItem.Id, Title = taskItem.Title.Value }, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<TaskItem?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            "SELECT id, title FROM task_items WHERE id = @Id",
            new { Id = id }, cancellationToken: cancellationToken);
        TaskItemRow? row = await connection.QuerySingleOrDefaultAsync<TaskItemRow>(command);
        return row is null ? null : TaskItem.Rehydrate(row.Id, row.Title);
    }

    private sealed class TaskItemRow
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
    }
}
