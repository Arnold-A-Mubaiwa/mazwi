using Mazwi.Application;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace Mazwi.WebApi.Controllers;

[ApiController]
[Route("api/stories/{id:guid}")]
public sealed class StoryReviewController(IStoryReviewService service) : ControllerBase
{
    [HttpPost("approvals/{language}")]
    public async Task<ActionResult<StoryDraftResponse>> Approve(Guid id, string language, ApproveLanguageRequest request, CancellationToken cancellationToken)
    {
        StoryDraftDto? story = await service.ApproveAsync(id, language, request.ReviewVersion, request.SpeakerChecked, cancellationToken);
        return story is null ? NotFound() : Ok(StoryDraftResponse.From(story));
    }

    [HttpPost("recordings/{language}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(26_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 25_000_000, ValueLengthLimit = 4096)]
    public async Task<ActionResult<StoryDraftResponse>> Upload(Guid id, string language, [FromForm] UploadTranslationRecordingRequest request, CancellationToken cancellationToken)
    {
        await using Stream stream = request.Recording.OpenReadStream();
        StoryDraftDto? story = await service.UploadRecordingAsync(new UploadTranslationRecordingCommand(id, language,
            request.Recording.FileName, request.Recording.Length, request.Permission, request.ReviewVersion), stream, cancellationToken);
        return story is null ? NotFound() : Ok(StoryDraftResponse.From(story));
    }

    [HttpGet("recordings/{language}")]
    public async Task<IActionResult> Play(Guid id, string language, CancellationToken cancellationToken)
    {
        RecordingPlaybackDto? playback = await service.GetRecordingAsync(id, language, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return playback is null ? NotFound() : File(playback.Content, playback.ContentType, enableRangeProcessing: true);
    }
}
