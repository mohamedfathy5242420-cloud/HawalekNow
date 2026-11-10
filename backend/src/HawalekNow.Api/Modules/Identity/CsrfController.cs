using HawalekNow.Api.Shared.Errors;
using HawalekNow.Api.Shared.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HawalekNow.Api.Modules.Identity;

public sealed record CsrfTokenResponse(string RequestToken, string HeaderName);
[ApiController, AllowAnonymous, Route("identity/csrf"), Tags("Identity")]
public sealed class CsrfController(IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<CsrfTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public IActionResult Token()
    {
        if (!Request.IsHttps) return ApiProblems.AsObjectResult(ApiProblems.Create(HttpContext, 403));
        Response.Headers.CacheControl = "no-store";
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new CsrfTokenResponse(tokens.RequestToken!, BrowserSecurity.CsrfHeader));
    }
}
