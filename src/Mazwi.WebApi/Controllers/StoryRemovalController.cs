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
public sealed class StoryRemovalController(IStoryRemovalService service) : ControllerBase
{
    [HttpPost("unpublish")]
    public async Task<ActionResult<StoryDraftResponse>> Unpublish(Guid id, ApproveStoryRequest request, CancellationToken cancellationToken)
    {
        StoryDraftDto? story = await service.UnpublishAsync(id, request.ReviewVersion, cancellationToken);
        return story is null ? NotFound() : Ok(StoryDraftResponse.From(story));
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(Guid id, Guid version, CancellationToken cancellationToken) =>
        await service.DeleteAsync(id, version, cancellationToken) ? NoContent() : NotFound();
}
