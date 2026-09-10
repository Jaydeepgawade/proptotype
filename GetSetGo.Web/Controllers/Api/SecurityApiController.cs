using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers.Api;

[ApiController,Route("api/v1/security")]
public sealed class SecurityApiController(IAntiforgery antiforgery):ControllerBase
{
    [HttpGet("antiforgery")]
    public IActionResult Antiforgery()
    {
        var tokens=antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new{requestToken=tokens.RequestToken,headerName=tokens.HeaderName});
    }
}
