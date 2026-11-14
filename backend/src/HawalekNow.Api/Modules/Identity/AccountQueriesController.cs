using System.Security.Claims;
using HawalekNow.Api.Shared.Errors;
using HawalekNow.Application.Modules.Identity;
using HawalekNow.Application.Modules.Identity.Contracts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HawalekNow.Api.Modules.Identity;

[ApiController, Route("identity"), Tags("Identity"), Authorize]
public sealed class AccountQueriesController(ISender sender, IAccountSessions sessions) : ControllerBase
{
    private Guid Owner => Guid.ParseExact(User.FindFirstValue("sub")!, "N");
    private Guid CurrentSession => Guid.ParseExact(User.FindFirstValue("sid")!, "N");
    [HttpGet("customer-profile"), Authorize(Policy = IdentityPolicies.VerifiedCustomer)]
    [ProducesResponseType<CustomerIdentityInfo>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> Customer(CancellationToken cancellationToken) => (await sender.Send(new MyCustomerIdentityQuery(Owner), cancellationToken)).ToActionResult(HttpContext);

    [HttpGet("sessions")]
    [ProducesResponseType<AccountSessionInfo[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> Sessions(CancellationToken cancellationToken) => (await sender.Send(new MySessionsQuery(Owner, CurrentSession), cancellationToken)).ToActionResult(HttpContext);

    [HttpGet("sessions/{sessionId:guid}")]
    [ProducesResponseType<AccountSessionInfo>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Session(Guid sessionId, CancellationToken cancellationToken) => (await sender.Send(new MySessionQuery(Owner, sessionId, CurrentSession), cancellationToken)).ToActionResult(HttpContext);

    [HttpGet("admin/session"), Authorize(Policy = IdentityPolicies.AdminOnly)]
    [ProducesResponseType<SessionActor>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> Admin(CancellationToken cancellationToken)
    {
        var actor = await sessions.FindActiveAsync(Owner, CurrentSession, cancellationToken);
        return actor is null ? ApiProblems.AsObjectResult(ApiProblems.Create(HttpContext, 401)) : Ok(actor);
    }
}
