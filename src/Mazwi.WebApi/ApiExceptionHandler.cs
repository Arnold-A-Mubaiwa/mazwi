using Mazwi.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Mazwi.WebApi;

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        bool invalidInput = exception is DomainException;
        context.Response.StatusCode = invalidInput ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = context.Response.StatusCode,
                Title = invalidInput ? "Invalid task." : "An unexpected error occurred.",
                Detail = invalidInput ? exception.Message : null,
            },
        });
        return true;
    }
}
