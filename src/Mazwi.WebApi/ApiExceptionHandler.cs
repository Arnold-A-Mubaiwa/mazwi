using Mazwi.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Mazwi.WebApi;

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        bool invalidInput = exception is DomainException;
        bool conflict = exception is StoryConflictException;
        context.Response.StatusCode = conflict ? StatusCodes.Status409Conflict : invalidInput ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = context.Response.StatusCode,
                Title = conflict ? "Story changed." : invalidInput ? "Invalid input." : "An unexpected error occurred.",
                Detail = invalidInput || conflict ? exception.Message : null,
            },
        });
        return true;
    }
}
