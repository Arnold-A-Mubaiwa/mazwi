using Mazwi.Application;

namespace Mazwi.WebApi.Models;

public sealed record StoryResponse(Guid Id, string Title, string ArtistName, string Community, string Country,
    string Type, string Language, string Status, DateTimeOffset PermissionRecordedAt)
{
    public static StoryResponse From(StoryDto story) => new(story.Id, story.Title, story.ArtistName, story.Community,
        story.Country, story.Type, story.Language, story.Status, story.PermissionRecordedAt);
}
