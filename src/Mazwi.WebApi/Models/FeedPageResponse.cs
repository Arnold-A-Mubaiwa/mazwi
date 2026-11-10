namespace Mazwi.WebApi.Models;

public sealed record FeedPageResponse(IReadOnlyList<StoryViewResponse> Items, int? NextOffset);
