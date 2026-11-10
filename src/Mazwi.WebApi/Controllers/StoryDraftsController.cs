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
    public async Task<ActionResult<StoryDraftResponse>> SaveTranslation(Guid id, string language, SaveTranslationDraftRequest request, CancellationToken cancellationToken)
    {
        StoryDraftDto? story = await service.SaveTranslationWithNotesAsync(id, language, request.Text, request.Notes, cancellationToken);
        return story is null ? NotFound() : Ok(StoryDraftResponse.From(story));
    }

    [HttpPost("translations/{language}/ai-draft")]
    public async Task<ActionResult<StoryDraftResponse>> AiDraft(Guid id, string language, ApproveStoryRequest request, CancellationToken cancellationToken)
    {
        StoryDraftDto? story = await service.DraftTranslationAsync(id, language, request.ReviewVersion, cancellationToken);
        return story is null ? NotFound() : Ok(StoryDraftResponse.From(story));
    }
}
