using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class ModerationService(IStoryRepository stories, IStoryReportRepository reports, TimeProvider clock) : IModerationService
{
    public async Task<IReadOnlyList<ReportedStoryDto>> ListAsync(CancellationToken cancellationToken) =>
        (await reports.ListOpenAsync(100, cancellationToken)).Select(ReportedStoryDto.From).ToArray();

    public Task<bool> HideAsync(Guid storyId, CancellationToken cancellationToken) =>
        ChangeAsync(storyId, story => story.HideForReview(), resolve: false, cancellationToken);

    public Task<bool> RestoreAsync(Guid storyId, CancellationToken cancellationToken) =>
        ChangeAsync(storyId, story => story.RestoreAfterReview(), resolve: true, cancellationToken);

    public Task<bool> ResolveAsync(Guid storyId, CancellationToken cancellationToken) =>
        ChangeAsync(storyId, null, resolve: true, cancellationToken);

    private async Task<bool> ChangeAsync(Guid storyId, Action<Story>? change, bool resolve, CancellationToken cancellationToken)
    {
        Story? story = await stories.GetAsync(storyId, cancellationToken);
        if (story is null)
        {
            return false;
        }

        if (change is not null)
        {
            change(story);
            await stories.SaveHiddenForReviewAsync(story, cancellationToken);
        }

        if (resolve)
        {
            await reports.ResolveAsync(storyId, clock.GetUtcNow(), cancellationToken);
        }

        return true;
    }
}
