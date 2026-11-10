using HawalekNow.Api.Shared.Errors;
using HawalekNow.Application.Modules.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HawalekNow.Api.Modules.Identity;

[ApiController]
[AllowAnonymous]
[Route("identity")]
[Tags("Identity")]
[RequestSizeLimit(8192)]
[EnableRateLimiting(IdentityRateLimits.Registration)]
public sealed class RegistrationController(ISender sender) : ControllerBase
{
    [HttpPost("customers/register")]
    [EndpointSummary("Request a separate customer account")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> Customer(RegisterCustomerCommand request, CancellationToken cancellationToken) =>
        (await sender.Send(request, cancellationToken)).ToActionResult(HttpContext);

    [HttpPost("merchants/register")]
    [EndpointSummary("Request a separate pending merchant account")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> Merchant(RegisterMerchantCommand request, CancellationToken cancellationToken) =>
        (await sender.Send(request, cancellationToken)).ToActionResult(HttpContext);
}
