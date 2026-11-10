using System.ComponentModel.DataAnnotations;
using Mazwi.Application;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace Mazwi.WebApi.Controllers;

[ApiController]
[Route("api/studio")]
public sealed class StoryStudioController(IStoryStudioService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<StudioPageResponse>> List(CancellationToken cancellationToken,
        [Range(0, 100000)] int offset = 0, [Range(1, 50)] int limit = 20)
    {
        StoryStudioPageDto page = await service.GetPageAsync(offset, limit, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(new StudioPageResponse(page.Items.Select(StoryResponse.From).ToArray(), page.NextOffset));
    }
}
