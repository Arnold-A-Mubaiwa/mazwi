namespace Mazwi.Domain;

public sealed class TaskItem
{
    public Guid Id { get; }
    public TaskTitle Title { get; private set; }

    private TaskItem(Guid id, TaskTitle title)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("A task must have an identity.");
        }

        ArgumentNullException.ThrowIfNull(title);
        Id = id;
        Title = title;
    }

    public static TaskItem Create(string title) => new(Guid.NewGuid(), new TaskTitle(title));
    public static TaskItem Rehydrate(Guid id, string title) => new(id, new TaskTitle(title));
    public void Rename(string title) => Title = new TaskTitle(title);
}
