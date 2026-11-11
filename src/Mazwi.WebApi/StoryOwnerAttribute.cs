using Mazwi.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mazwi.WebApi;

// Lets the action run only for the signed-in owner of the story named by the {id} route value.
// It is an authorization filter, so it runs before model validation and other artists learn nothing about the request.
public sealed class StoryOwnerAttribute() : TypeFilterAttribute(typeof(StoryOwnerFilter));

public sealed class StoryOwnerFilter(IStoryAccessService access) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            // [Authorize] answers signed-out requests with 401.
            return;
        }

        if (!Guid.TryParse(context.RouteData.Values["id"]?.ToString(), out Guid storyId))
        {
            context.Result = new NotFoundResult();
            return;
        }

        StoryAccess result = await access.CheckOwnerAsync(storyId, context.HttpContext.User.ArtistId(), context.HttpContext.RequestAborted);
        if (result == StoryAccess.Allowed)
        {
            return;
        }

        context.Result = result == StoryAccess.NotFound ? new NotFoundResult() : new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Not your story.",
            Detail = "This story belongs to another artist.",
        })
        { StatusCode = StatusCodes.Status403Forbidden };
    }
}
