using Mazwi.Application;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mazwi.WebApi.Controllers;

[ApiController]
[Authorize]
[StoryOwner]
[Route("api/stories/{id:guid}")]
[RequestSizeLimit(10_000)]
public sealed class StoryTwinsController(IStoryTwinService service) : ControllerBase
{
    [HttpPost("twins/{twinId:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, Guid twinId, CancellationToken cancellationToken) =>
        await service.ConfirmAsync(id, twinId, cancellationToken) ? NoContent() : NotFound();

    /// <summary>Rejects a suggestion or unlinks a confirmed twin. Final: the pair is never suggested again.</summary>
    [HttpPost("twins/{twinId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, Guid twinId, CancellationToken cancellationToken) =>
        await service.RejectAsync(id, twinId, cancellationToken) ? NoContent() : NotFound();

    [HttpPut("twinning")]
    public async Task<ActionResult<StoryDraftResponse>> SetTwinning(Guid id, TwinsAllowedRequest request, CancellationToken cancellationToken)
    {
        StoryDraftDto? story = await service.SetTwinsAllowedAsync(id, request.Allowed!.Value, cancellationToken);
        return story is null ? NotFound() : Ok(StoryDraftResponse.From(story));
    }
}
