using Mazwi.Application;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace Mazwi.WebApi.Controllers;

[ApiController]
[Route("api/stories/{id:guid}/drafts/translations/{language}/ai-voice")]
[RequestSizeLimit(10_000)]
public sealed class StoryVoiceController(IStoryVoiceService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<StoryDraftResponse>> Generate(Guid id, string language, ApproveStoryRequest request, CancellationToken cancellationToken)
    {
        StoryDraftDto? story = await service.GenerateVoiceAsync(id, language, request.ReviewVersion, cancellationToken);
        return story is null ? NotFound() : Ok(StoryDraftResponse.From(story));
    }
}
