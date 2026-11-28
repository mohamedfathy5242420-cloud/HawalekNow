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

[ApiController, Route("admin/merchants"), Tags("Merchant Reviews"), Authorize(Policy = IdentityPolicies.AdminOnly), RequestSizeLimit(8192)]
[ProducesResponseType<ProblemDetails>(400, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
public sealed class MerchantReviewAdminController(ISender sender) : ControllerBase
{
    private Guid Actor => Guid.ParseExact(User.FindFirstValue("sub")!, "N");
    [HttpGet, ProducesResponseType<MerchantPage<MerchantReviewDto>>(200)]
    public async Task<IActionResult> List(string? status = null, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default) =>
        (await sender.Send(new ListMerchantReviewsQuery(Actor, status, page, pageSize), cancellationToken)).ToActionResult(HttpContext);
    [HttpGet("{id:guid}/audit"), ProducesResponseType<MerchantPage<MerchantAuditDto>>(200)]
    public async Task<IActionResult> Audit(Guid id, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default) =>
        (await sender.Send(new ListMerchantAuditQuery(Actor, id, page, pageSize), cancellationToken)).ToActionResult(HttpContext);
    [HttpPost("{id:guid}/approve"), EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<MerchantReviewDto>(200)]
    public Task<IActionResult> Approve(Guid id, MerchantReviewWrite value, CancellationToken cancellationToken) => Decide(id, MerchantReviewAction.Approve, value, cancellationToken);
    [HttpPost("{id:guid}/request-changes"), EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<MerchantReviewDto>(200)]
    public Task<IActionResult> RequestChanges(Guid id, MerchantReviewWrite value, CancellationToken cancellationToken) => Decide(id, MerchantReviewAction.RequestChanges, value, cancellationToken);
    [HttpPost("{id:guid}/reject"), EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<MerchantReviewDto>(200)]
    public Task<IActionResult> Reject(Guid id, MerchantReviewWrite value, CancellationToken cancellationToken) => Decide(id, MerchantReviewAction.Reject, value, cancellationToken);
    [HttpPost("{id:guid}/suspend"), EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<MerchantReviewDto>(200)]
    public Task<IActionResult> Suspend(Guid id, MerchantReviewWrite value, CancellationToken cancellationToken) => Decide(id, MerchantReviewAction.Suspend, value, cancellationToken);
    [HttpPost("{id:guid}/reinstate"), EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<MerchantReviewDto>(200)]
    public Task<IActionResult> Reinstate(Guid id, MerchantReviewWrite value, CancellationToken cancellationToken) => Decide(id, MerchantReviewAction.Reinstate, value, cancellationToken);
    private async Task<IActionResult> Decide(Guid id, MerchantReviewAction action, MerchantReviewWrite value, CancellationToken cancellationToken) =>
        (await sender.Send(new ReviewMerchantCommand(Actor, id, action, value), cancellationToken)).ToActionResult(HttpContext);
}

[ApiController, Route("merchants/review"), Tags("Merchant Reviews"), Authorize, RequestSizeLimit(8192)]
[ProducesResponseType<ProblemDetails>(400, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
public sealed class MerchantOwnReviewController(ISender sender) : ControllerBase
{
    private Guid Actor => Guid.ParseExact(User.FindFirstValue("sub")!, "N");
    [HttpGet, ProducesResponseType<MerchantReviewDto>(200)]
    public async Task<IActionResult> Own(CancellationToken cancellationToken) =>
        (await sender.Send(new GetOwnMerchantReviewQuery(Actor), cancellationToken)).ToActionResult(HttpContext);
    [HttpPost("resubmit"), EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<MerchantReviewDto>(200)]
    public async Task<IActionResult> Resubmit(MerchantResubmitWrite value, CancellationToken cancellationToken) =>
        (await sender.Send(new ResubmitMerchantCommand(Actor, value.RowVersion), cancellationToken)).ToActionResult(HttpContext);
}
