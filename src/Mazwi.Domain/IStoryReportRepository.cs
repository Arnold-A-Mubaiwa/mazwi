namespace Mazwi.Domain;

public interface IStoryReportRepository
{
    Task AddAsync(StoryReport report, CancellationToken cancellationToken);
    // Stories with unresolved reports, newest report first.
    Task<IReadOnlyList<ReportedStory>> ListOpenAsync(int limit, CancellationToken cancellationToken);
    Task ResolveAsync(Guid storyId, DateTimeOffset resolvedAt, CancellationToken cancellationToken);
}
