using Mazwi.Application;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mazwi.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/stories")]
public sealed class StoriesController(IStoryService service) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(26_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 25_000_000, ValueLengthLimit = 4096)]
    public async Task<ActionResult<StoryResponse>> Upload([FromForm] UploadStoryRequest request, CancellationToken cancellationToken)
    {
        await using Stream stream = request.Recording.OpenReadStream();
        StoryDto story = await service.UploadAsync(new UploadStoryCommand(request.Title, request.ArtistName,
            request.Community, request.Country, request.Type, request.Language, request.Permission,
            request.Recording.FileName, request.Recording.Length, User.ArtistId()), stream, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = story.Id }, StoryResponse.From(story));
    }

    [HttpGet("{id:guid}")]
    [StoryOwner]
    public async Task<ActionResult<StoryResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        StoryDto? story = await service.GetAsync(id, cancellationToken);
        return story is null ? NotFound() : Ok(StoryResponse.From(story));
    }
}
