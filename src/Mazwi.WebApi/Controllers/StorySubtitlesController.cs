using Mazwi.Application;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mazwi.WebApi.Controllers;

[ApiController]
[Authorize]
[StoryOwner]
[Route("api/stories/{id:guid}/subtitles/{language}")]
[RequestSizeLimit(100_000)]
public sealed class StorySubtitlesController(IStorySubtitleService service) : ControllerBase
{
    [HttpPut]
    public async Task<ActionResult<StoryDraftResponse>> Save(Guid id, string language, SaveSubtitleTimingRequest request, CancellationToken cancellationToken)
    {
        StoryDraftDto? story = await service.SaveTimingAsync(id, language, request.StartsMs, request.EndMs, request.ReviewVersion, cancellationToken);
        return story is null ? NotFound() : Ok(StoryDraftResponse.From(story));
    }
}
