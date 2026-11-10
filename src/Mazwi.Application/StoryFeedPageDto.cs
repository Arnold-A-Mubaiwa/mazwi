namespace Mazwi.Application;

public sealed record StoryFeedPageDto(IReadOnlyList<StoryViewDto> Items, int? NextOffset);
