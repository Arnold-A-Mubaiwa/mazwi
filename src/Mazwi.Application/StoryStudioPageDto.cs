namespace Mazwi.Application;

public sealed record StoryStudioPageDto(IReadOnlyList<StoryDto> Items, int? NextOffset);
