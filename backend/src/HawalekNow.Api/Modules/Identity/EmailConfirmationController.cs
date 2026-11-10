using HawalekNow.Api.Shared.Errors;
using HawalekNow.Application.Modules.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HawalekNow.Api.Modules.Identity;

[ApiController, AllowAnonymous, Route("identity/email"), Tags("Identity"), RequestSizeLimit(8192)]
public sealed class EmailConfirmationController(ISender sender) : ControllerBase
{
    [HttpPost("request")]
    [EnableRateLimiting(IdentityRateLimits.SendCode)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> RequestCode(RequestEmailCodeCommand request, CancellationToken cancellationToken) => (await sender.Send(request, cancellationToken)).ToActionResult(HttpContext);
    [HttpPost("verify")]
    [EnableRateLimiting(IdentityRateLimits.VerifyCode)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Verify(VerifyEmailCodeCommand request, CancellationToken cancellationToken) => (await sender.Send(request, cancellationToken)).ToActionResult(HttpContext);
}
