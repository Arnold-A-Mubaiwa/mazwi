using Mazwi.Application;
using Mazwi.Domain;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mazwi.WebApi.Controllers;

[ApiController]
[Route("api/feed/{id:guid}/reports")]
[RequestSizeLimit(5_000)]
public sealed class StoryReportsController(IStoryReportService service) : ControllerBase
{
    // Public: anyone watching can report. Nothing about the reporter is stored.
    [HttpPost]
    [EnableRateLimiting(ReportLimits.Policy)]
    public async Task<IActionResult> Report(Guid id, ReportStoryRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(request.Reason, ignoreCase: true, out ReportReason reason) || !Enum.IsDefined(reason))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid input.", detail: "Choose one of the listed reasons.");
        }

        return await service.ReportAsync(id, reason, request.Note, cancellationToken) ? Accepted() : NotFound();
    }
}
