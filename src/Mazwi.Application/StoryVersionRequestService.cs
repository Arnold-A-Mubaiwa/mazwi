using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class StoryVersionRequestService(IStoryRepository stories, IStoryVersionRequestRepository requests) : IStoryVersionRequestService
{
    public async Task<bool> RequestAsync(Guid storyId, string language, CancellationToken cancellationToken)
    {
        Story? story = await stories.GetAsync(storyId, cancellationToken);
        if (story is null || !story.CanBeReported)
        {
            return false;
        }

        await requests.AddAsync(VersionRequest.For(story, language), cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<VersionRequestDto>> ListForOwnerAsync(Guid ownerId, CancellationToken cancellationToken) =>
        (await requests.ListForOwnerAsync(ownerId, cancellationToken)).Select(total => new VersionRequestDto(total.StoryId, total.Language, total.Count)).ToArray();
}
