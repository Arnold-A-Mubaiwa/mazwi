using Mazwi.Application;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace Mazwi.WebApi.Controllers;

[ApiController]
[Route("api/task-items")]
public sealed class TaskItemsController(ITaskItemService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TaskItemResponse>> Create(CreateTaskItemRequest request, CancellationToken cancellationToken)
    {
        TaskItemDto taskItem = await service.CreateAsync(request.Title, cancellationToken);
        var response = new TaskItemResponse(taskItem.Id, taskItem.Title);
        return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskItemResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        TaskItemDto? taskItem = await service.GetAsync(id, cancellationToken);
        return taskItem is null ? NotFound() : Ok(new TaskItemResponse(taskItem.Id, taskItem.Title));
    }
}
