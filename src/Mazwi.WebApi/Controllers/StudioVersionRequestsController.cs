using Mazwi.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mazwi.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/studio/version-requests")]
public sealed class StudioVersionRequestsController(IStoryVersionRequestService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VersionRequestDto>>> List(CancellationToken cancellationToken)
    {
        IReadOnlyList<VersionRequestDto> requests = await service.ListForOwnerAsync(User.ArtistId(), cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(requests);
    }
}
