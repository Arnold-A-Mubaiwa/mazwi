using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class StoryStatsService(IStoryRepository stories, IStoryPlayRepository plays, TimeProvider clock) : IStoryStatsService
{
    public async Task<bool> RecordPlayAsync(Guid id, string language, Guid version, CancellationToken cancellationToken)
    {
        Story? story = await stories.GetAsync(id, cancellationToken);
        StoryPlay? play = story?.RecordPlay(language, version, Today());
        if (play is null)
        {
            return false;
        }

        await plays.RecordAsync(play, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<StoryStatsDto>> GetForOwnerAsync(Guid ownerId, CancellationToken cancellationToken) =>
        StoryStatsDto.From(await plays.ListForOwnerAsync(ownerId, Today().AddDays(-6), cancellationToken));

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
}
