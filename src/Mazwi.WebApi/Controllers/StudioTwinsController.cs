using Mazwi.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mazwi.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/studio/twins")]
public sealed class StudioTwinsController(IStoryTwinService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StoryTwinDto>>> List(CancellationToken cancellationToken)
    {
        IReadOnlyList<StoryTwinDto> twins = await service.ListForOwnerAsync(User.ArtistId(), cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(twins);
    }
}
