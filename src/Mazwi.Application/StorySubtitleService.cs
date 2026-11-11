using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class StorySubtitleService(IStoryRepository repository) : IStorySubtitleService
{
    public async Task<StoryDraftDto?> SaveTimingAsync(Guid id, string language, IReadOnlyList<int> startsMs, int endMs, Guid reviewVersion, CancellationToken cancellationToken)
    {
        Story? story = await repository.GetAsync(id, cancellationToken);
        if (story is null)
        {
            return null;
        }

        story.SetSubtitleTiming(language, new SubtitleTiming(startsMs, endMs), reviewVersion);
        await repository.SaveSubtitleTimingAsync(story, language, reviewVersion, cancellationToken);
        return StoryDraftDto.From(story);
    }
}
