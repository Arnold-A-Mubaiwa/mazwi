using Mazwi.Application;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mazwi.WebApi.Controllers;

// Public: viewers find confirmed twins and ask for versions in their language. Nothing about the listener is stored.
[ApiController]
[Route("api/feed/{id:guid}")]
[RequestSizeLimit(5_000)]
public sealed class StoryTwinsFeedController(IStoryTwinService twins, IStoryVersionRequestService requests) : ControllerBase
{
    [HttpGet("twins")]
    public async Task<ActionResult<IReadOnlyList<ViewerTwinDto>>> List(Guid id, CancellationToken cancellationToken, string language = "sn")
    {
        IReadOnlyList<ViewerTwinDto>? result = await twins.ListForViewerAsync(id, language, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store";
        return Ok(result);
    }

    [HttpPost("version-requests")]
    [EnableRateLimiting(VersionRequestLimits.Policy)]
    public async Task<IActionResult> AskForVersion(Guid id, VersionRequestRequest request, CancellationToken cancellationToken) =>
        await requests.RequestAsync(id, request.Language, cancellationToken) ? Accepted() : NotFound();
}
