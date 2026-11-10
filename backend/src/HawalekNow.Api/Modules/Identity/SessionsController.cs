using System.Security.Claims;
using HawalekNow.Api.Shared.Errors;
using HawalekNow.Application.Modules.Identity;
using HawalekNow.Application.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HawalekNow.Api.Modules.Identity;

[ApiController, Route("identity"), Tags("Identity"), RequestSizeLimit(8192)]
public sealed class SessionsController(ISender sender, IAccountSessions sessions) : ControllerBase
{
    [HttpPost("login"), AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Login(LoginCommand request, CancellationToken cancellationToken)
    {
        if (!Request.IsHttps) return ApiProblems.AsObjectResult(ApiProblems.Create(HttpContext, 403));
        return Complete(await sender.Send(request, cancellationToken));
    }
    [HttpPost("refresh"), AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.IsHttps) return ApiProblems.AsObjectResult(ApiProblems.Create(HttpContext, 403));
        return Complete(await sender.Send(new RefreshSessionCommand(Request.Cookies[IdentityAuthentication.RefreshCookie]), cancellationToken));
    }
    [HttpGet("me"), Authorize]
    [ProducesResponseType<SessionActor>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var actor = await sessions.FindActiveAsync(Guid.ParseExact(User.FindFirstValue("sub")!, "N"), Guid.ParseExact(User.FindFirstValue("sid")!, "N"), cancellationToken);
        return actor is null ? ApiProblems.AsObjectResult(ApiProblems.Create(HttpContext, 401)) : Ok(actor);
    }

    private IActionResult Complete(Result<SessionCredentials> result)
    {
        Response.Headers.CacheControl = "no-store";
        if (!result.IsSuccess)
        {
            IdentityAuthentication.ClearCookies(Response);
            return ApiProblems.AsObjectResult(ApiProblems.FromError(HttpContext, result.Error!));
        }
        IdentityAuthentication.WriteCookies(Response, result.Value); return NoContent();
    }
}
