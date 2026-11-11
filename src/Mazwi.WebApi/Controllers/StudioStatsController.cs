using Mazwi.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mazwi.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/studio/stats")]
public sealed class StudioStatsController(IStoryStatsService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StoryStatsDto>>> Get(CancellationToken cancellationToken)
    {
        IReadOnlyList<StoryStatsDto> stats = await service.GetForOwnerAsync(User.ArtistId(), cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(stats);
    }
}
