namespace Mazwi.Application;

public sealed record VersionRequestDto(Guid StoryId, string Language, int Count);
