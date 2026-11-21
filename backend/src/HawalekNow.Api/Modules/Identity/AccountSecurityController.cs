using System.Security.Claims;
using HawalekNow.Api.Shared.Errors;
using HawalekNow.Application.Modules.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HawalekNow.Api.Modules.Identity;

[ApiController, Route("identity"), Tags("Identity"), RequestSizeLimit(8192)]
public sealed class AccountSecurityController(ISender sender) : ControllerBase
{
    [HttpPost("password/request"), AllowAnonymous, EnableRateLimiting(IdentityRateLimits.SendCode)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> RequestCode(RequestPasswordResetCommand request, CancellationToken cancellationToken) => (await sender.Send(request, cancellationToken)).ToActionResult(HttpContext);

    [HttpPost("password/reset"), AllowAnonymous, EnableRateLimiting(IdentityRateLimits.VerifyCode)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Reset(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        if (result.IsSuccess) IdentityAuthentication.ClearCookies(Response);
        return result.ToActionResult(HttpContext);
    }
    [HttpPost("logout"), AllowAnonymous, EnableRateLimiting(IdentityRateLimits.SessionMutation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new LogoutCommand(Request.Cookies[IdentityAuthentication.RefreshCookie]), cancellationToken);
        IdentityAuthentication.ClearCookies(Response); return result.ToActionResult(HttpContext);
    }
    [HttpPost("logout-all"), Authorize, EnableRateLimiting(IdentityRateLimits.SessionMutation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new LogoutAllCommand(Guid.ParseExact(User.FindFirstValue("sub")!, "N")), cancellationToken);
        if (result.IsSuccess) IdentityAuthentication.ClearCookies(Response); return result.ToActionResult(HttpContext);
    }
    [HttpDelete("sessions/{sessionId:guid}"), Authorize, EnableRateLimiting(IdentityRateLimits.SessionMutation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Revoke(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RevokeSessionCommand(Guid.ParseExact(User.FindFirstValue("sub")!, "N"), sessionId), cancellationToken);
        if (result.IsSuccess && sessionId == Guid.ParseExact(User.FindFirstValue("sid")!, "N")) IdentityAuthentication.ClearCookies(Response);
        return result.ToActionResult(HttpContext);
    }
}
