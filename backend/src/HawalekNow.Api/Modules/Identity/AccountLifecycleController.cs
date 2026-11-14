using System.Security.Claims;
using HawalekNow.Api.Shared.Errors;
using HawalekNow.Application.Modules.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HawalekNow.Api.Modules.Identity;

public sealed record DeleteAccountRequest(string Password);
[ApiController, Route("identity"), Tags("Identity"), RequestSizeLimit(8192)]
public sealed class AccountLifecycleController(ISender sender) : ControllerBase
{
    [HttpDelete("account"), Authorize, EnableRateLimiting(IdentityRateLimits.SessionMutation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> Delete(DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteAccountCommand(Guid.ParseExact(User.FindFirstValue("sub")!, "N"), request.Password), cancellationToken);
        if (result.IsSuccess) IdentityAuthentication.ClearCookies(Response);
        return result.ToActionResult(HttpContext);
    }
    [HttpPost("recovery/request"), AllowAnonymous, EnableRateLimiting(IdentityRateLimits.SendCode)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> RequestRecovery(RequestAccountRecoveryCommand request, CancellationToken cancellationToken) => (await sender.Send(request, cancellationToken)).ToActionResult(HttpContext);

    [HttpPost("recovery/verify"), AllowAnonymous, EnableRateLimiting(IdentityRateLimits.VerifyCode)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Recover(RecoverAccountCommand request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        if (result.IsSuccess) IdentityAuthentication.ClearCookies(Response);
        return result.ToActionResult(HttpContext);
    }
}
