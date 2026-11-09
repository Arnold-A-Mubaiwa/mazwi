using Microsoft.AspNetCore.Mvc;

namespace Mazwi.WebApi.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public ActionResult<HealthResponse> Get() => Ok(new HealthResponse("ok", "Mazwi"));
}

public sealed record HealthResponse(string Status, string Service);
