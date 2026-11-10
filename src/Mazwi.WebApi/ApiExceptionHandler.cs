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
        bool notAllowed = exception is AiDraftNotAllowedException;
        bool upstream = exception is TranslationDraftUnavailableException;
        context.Response.StatusCode = exception is TranslationDraftUnavailableException { TimedOut: true } ? StatusCodes.Status504GatewayTimeout
            : upstream ? StatusCodes.Status502BadGateway : notAllowed ? StatusCodes.Status422UnprocessableEntity
            : conflict ? StatusCodes.Status409Conflict : invalidInput ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = context.Response.StatusCode,
                Title = upstream ? "AI translation unavailable." : notAllowed ? "AI draft not allowed." : conflict ? "Story changed." : invalidInput ? "Invalid input." : "An unexpected error occurred.",
                Detail = invalidInput || conflict || notAllowed || upstream ? exception.Message : null,
            },
        });
        return true;
    }
}
