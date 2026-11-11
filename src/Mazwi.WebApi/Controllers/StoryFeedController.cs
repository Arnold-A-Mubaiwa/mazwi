using System.ComponentModel.DataAnnotations;
using Mazwi.Application;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace Mazwi.WebApi.Controllers;

[ApiController]
[Route("api/feed")]
public sealed class StoryFeedController(IStoryFeedService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<FeedPageResponse>> List(CancellationToken cancellationToken, string language = "sn",
        [Range(0, 100000)] int offset = 0, [Range(1, 50)] int limit = 20)
    {
        StoryFeedPageDto page = await service.GetPageAsync(language, offset, limit, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(new FeedPageResponse(page.Items.Select(StoryViewResponse.From).ToArray(), page.NextOffset));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StoryViewResponse>> Get(Guid id, CancellationToken cancellationToken, string language = "sn")
    {
        StoryViewDto? story = await service.GetAsync(id, language, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return story is null ? NotFound() : Ok(StoryViewResponse.From(story));
    }

    [HttpGet("{id:guid}/recording")]
    public async Task<IActionResult> Play(Guid id, Guid version, CancellationToken cancellationToken, string language = "sn", bool originalAudio = false)
    {
        RecordingPlaybackDto? recording = await service.GetRecordingAsync(id, language, version, originalAudio, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return recording is null ? NotFound() : File(recording.Content, recording.ContentType, enableRangeProcessing: true);
    }

    [HttpGet("{id:guid}/subtitles")]
    public async Task<IActionResult> Subtitles(Guid id, Guid version, CancellationToken cancellationToken, string language = "sn", bool originalAudio = false)
    {
        IReadOnlyList<SubtitleCueDto>? cues = await service.GetSubtitlesAsync(id, language, version, originalAudio, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return cues is null ? NotFound() : Content(WebVtt.Format(cues), "text/vtt; charset=utf-8");
    }
}
