using System.Security.Claims;
using HawalekNow.Api.Shared.Errors;
using HawalekNow.Api.Shared.Security;
using HawalekNow.Application.Modules.Identity.Contracts;
using HawalekNow.Application.Modules.Merchants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HawalekNow.Api.Modules.Merchants;

[ApiController, Route("merchants/stores/{storeId:guid}/activity-change-requests"), Tags("Store Activity Changes"), Authorize(Policy = IdentityPolicies.VerifiedMerchantSetup), RequestSizeLimit(4096)]
[ProducesResponseType<ProblemDetails>(400, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
public sealed class OwnerActivityChangeController(ISender sender) : ControllerBase
{
    private Guid Actor => Guid.ParseExact(User.FindFirstValue("sub")!, "N");
    [HttpPost, EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<ActivityChangeDto>(200)]
    public async Task<IActionResult> Create(Guid storeId, ActivityChangeWrite value, CancellationToken cancellationToken) =>
        (await sender.Send(new RequestActivityChangeCommand(Actor, storeId, value), cancellationToken)).ToActionResult(HttpContext);
    [HttpGet, ProducesResponseType<MerchantPage<ActivityChangeDto>>(200)]
    public async Task<IActionResult> List(Guid storeId, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default) =>
        (await sender.Send(new OwnActivityChangesQuery(Actor, storeId, page, pageSize), cancellationToken)).ToActionResult(HttpContext);
}

[ApiController, Route("admin/store-activity-changes"), Tags("Administrative Store Activity Changes"), Authorize(Policy = IdentityPolicies.AdminOnly), RequestSizeLimit(4096)]
[ProducesResponseType<ProblemDetails>(400, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
public sealed class AdminActivityChangeController(ISender sender) : ControllerBase
{
    private Guid Actor => Guid.ParseExact(User.FindFirstValue("sub")!, "N");
    [HttpGet, ProducesResponseType<MerchantPage<ActivityChangeDto>>(200)]
    public async Task<IActionResult> List(string? status = null, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default) =>
        (await sender.Send(new AdminActivityChangesQuery(Actor, status, page, pageSize), cancellationToken)).ToActionResult(HttpContext);
    [HttpPost("{id:guid}/approve"), EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<ActivityChangeDto>(200)]
    public Task<IActionResult> Approve(Guid id, ActivityChangeReviewWrite value, CancellationToken cancellationToken) => Review(id, true, value, cancellationToken);
    [HttpPost("{id:guid}/reject"), EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<ActivityChangeDto>(200)]
    public Task<IActionResult> Reject(Guid id, ActivityChangeReviewWrite value, CancellationToken cancellationToken) => Review(id, false, value, cancellationToken);
    private async Task<IActionResult> Review(Guid id, bool approve, ActivityChangeReviewWrite value, CancellationToken cancellationToken) =>
        (await sender.Send(new ReviewActivityChangeCommand(Actor, id, approve, value), cancellationToken)).ToActionResult(HttpContext);
}
