using Mazwi.Application;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace Mazwi.WebApi.Controllers;

[ApiController]
[Route("api/stories/{id:guid}/drafts")]
[RequestSizeLimit(200_000)]
public sealed class StoryDraftsController(IStoryDraftService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<StoryDraftResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        StoryDraftDto? story = await service.GetAsync(id, cancellationToken);
        return story is null ? NotFound() : Ok(StoryDraftResponse.From(story));
    }

    [HttpPut("transcript")]
    public async Task<ActionResult<StoryDraftResponse>> SaveTranscript(Guid id, SaveStoryTextRequest request, CancellationToken cancellationToken)
    {
        StoryDraftDto? story = await service.SaveTranscriptAsync(id, request.Text, cancellationToken);
        return story is null ? NotFound() : Ok(StoryDraftResponse.From(story));
    }

    [HttpPut("translations/{language}")]
    public async Task<ActionResult<StoryDraftResponse>> SaveTranslation(Guid id, string language, SaveStoryTextRequest request, CancellationToken cancellationToken)
    {
        StoryDraftDto? story = await service.SaveTranslationAsync(id, language, request.Text, cancellationToken);
        return story is null ? NotFound() : Ok(StoryDraftResponse.From(story));
    }
}
