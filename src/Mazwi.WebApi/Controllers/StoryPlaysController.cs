using Mazwi.Application;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mazwi.WebApi.Controllers;

[ApiController]
[Route("api/feed/{id:guid}/plays")]
[RequestSizeLimit(1_000)]
public sealed class StoryPlaysController(IStoryStatsService service) : ControllerBase
{
    // Public and anonymous: nothing about the viewer is stored, only the story, language and day.
    [HttpPost]
    [EnableRateLimiting(PlayLimits.Policy)]
    public async Task<IActionResult> Record(Guid id, RecordPlayRequest request, CancellationToken cancellationToken) =>
        await service.RecordPlayAsync(id, request.Language, request.Version, cancellationToken) ? NoContent() : NotFound();
}
