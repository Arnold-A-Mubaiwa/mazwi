using Mazwi.Application;
using Microsoft.AspNetCore.Mvc;

namespace Mazwi.WebApi.Controllers;

[ApiController]
[Route("api/languages")]
public sealed class LanguagesController(ILanguageService service) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<LanguageDto>> List()
    {
        Response.Headers.CacheControl = "public, max-age=3600";
        return Ok(service.List());
    }
}
