using Mazwi.Domain;

namespace Mazwi.Application;

public sealed record StoryDto(Guid Id, string Title, string ArtistName, string Community, string Country,
    string Type, string Language, string Status, DateTimeOffset PermissionRecordedAt)
{
    public static StoryDto From(Story story) => new(story.Id, story.Details.Title, story.Details.ArtistName,
        story.Details.Community, story.Details.Country, story.Details.Type, story.Details.Language,
        story.Status, story.PermissionRecordedAt);
}
