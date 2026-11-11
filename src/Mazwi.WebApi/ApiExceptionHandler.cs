using Mazwi.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Mazwi.WebApi;

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        bool invalidInput = exception is DomainException;
        bool conflict = exception is StoryConflictException or AccountExistsException;
        bool forbidden = exception is StoryAccessDeniedException;
        bool notAllowed = exception is AiDraftNotAllowedException;
        bool upstream = exception is TranslationDraftUnavailableException or SpeechUnavailableException or TranscriptionUnavailableException;
        bool timedOut = exception is TranslationDraftUnavailableException { TimedOut: true } or SpeechUnavailableException { TimedOut: true }
            or TranscriptionUnavailableException { TimedOut: true };
        context.Response.StatusCode = timedOut ? StatusCodes.Status504GatewayTimeout
            : upstream ? StatusCodes.Status502BadGateway : notAllowed ? StatusCodes.Status422UnprocessableEntity
            : forbidden ? StatusCodes.Status403Forbidden : conflict ? StatusCodes.Status409Conflict : invalidInput ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = context.Response.StatusCode,
                Title = upstream ? "AI service unavailable." : notAllowed ? "AI request not allowed." : forbidden ? "Not your story." : exception is AccountExistsException ? "Account exists." : conflict ? "Story changed." : invalidInput ? "Invalid input." : "An unexpected error occurred.",
                Detail = invalidInput || conflict || forbidden || notAllowed || upstream ? exception.Message : null,
            },
        });
        return true;
    }
}
