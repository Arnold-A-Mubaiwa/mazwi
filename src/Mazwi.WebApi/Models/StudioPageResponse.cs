namespace Mazwi.WebApi.Models;

public sealed record StudioPageResponse(IReadOnlyList<StoryResponse> Items, int? NextOffset);
