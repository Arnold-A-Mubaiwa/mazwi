using Mazwi.Domain;

namespace Mazwi.Application;

public sealed record ReportedStoryDto(Guid StoryId, string Title, string ArtistName, bool HiddenForReview, int OpenReports,
    IReadOnlyList<string> Reasons, IReadOnlyList<string> Notes, DateTimeOffset LatestReportAt)
{
    public static ReportedStoryDto From(ReportedStory story) => new(story.StoryId, story.Title, story.ArtistName, story.HiddenForReview,
        story.OpenReports, story.Reasons.Select(reason => reason.ToString()).ToArray(), story.Notes, story.LatestReportAt);
}

public interface IModerationService
{
    Task<IReadOnlyList<ReportedStoryDto>> ListAsync(CancellationToken cancellationToken);
    // Each returns false when the story does not exist.
    Task<bool> HideAsync(Guid storyId, CancellationToken cancellationToken);
    // Puts the story back in the feed and closes its reports.
    Task<bool> RestoreAsync(Guid storyId, CancellationToken cancellationToken);
    // Closes the reports and leaves the story as it is (visible, or hidden for good).
    Task<bool> ResolveAsync(Guid storyId, CancellationToken cancellationToken);
}
