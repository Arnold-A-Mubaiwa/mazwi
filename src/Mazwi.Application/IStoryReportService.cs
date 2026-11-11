using Mazwi.Domain;

namespace Mazwi.Application;

public interface IStoryReportService
{
    // False when the story does not exist or is not in the feed.
    Task<bool> ReportAsync(Guid storyId, ReportReason reason, string? note, CancellationToken cancellationToken);
}
