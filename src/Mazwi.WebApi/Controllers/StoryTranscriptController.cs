using Mazwi.Application;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace Mazwi.WebApi.Controllers;

[ApiController]
[Route("api/stories/{id:guid}/drafts/transcript/ai-draft")]
[RequestSizeLimit(10_000)]
public sealed class StoryTranscriptController(IStoryTranscriptService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<StoryDraftResponse>> Draft(Guid id, ApproveStoryRequest request, CancellationToken cancellationToken)
    {
        StoryDraftDto? story = await service.DraftTranscriptAsync(id, request.ReviewVersion, cancellationToken);
        return story is null ? NotFound() : Ok(StoryDraftResponse.From(story));
    }
}
