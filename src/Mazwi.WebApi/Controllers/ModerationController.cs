using Mazwi.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mazwi.WebApi.Controllers;

[ApiController]
[Authorize(Policy = ModeratorRequirement.Policy)]
[Route("api/moderation")]
public sealed class ModerationController(IModerationService service) : ControllerBase
{
    [HttpGet("reports")]
    public async Task<ActionResult<IReadOnlyList<ReportedStoryDto>>> Reports(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await service.ListAsync(cancellationToken));
    }

    [HttpPost("stories/{id:guid}/hide")]
    public async Task<IActionResult> Hide(Guid id, CancellationToken cancellationToken) =>
        await service.HideAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("stories/{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken) =>
        await service.RestoreAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("stories/{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, CancellationToken cancellationToken) =>
        await service.ResolveAsync(id, cancellationToken) ? NoContent() : NotFound();
}
