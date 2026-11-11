using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class StoryReportService(IStoryRepository stories, IStoryReportRepository reports, TimeProvider clock) : IStoryReportService
{
    public async Task<bool> ReportAsync(Guid storyId, ReportReason reason, string? note, CancellationToken cancellationToken)
    {
        Story? story = await stories.GetAsync(storyId, cancellationToken);
        if (story is null || !story.CanBeReported)
        {
            return false;
        }

        await reports.AddAsync(StoryReport.File(story, reason, note, clock.GetUtcNow()), cancellationToken);
        return true;
    }
}
