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

[ApiController, Route("merchants/stores/{storeId:guid}/branches"), Tags("Branches and Pickup Points"), Authorize(Policy = IdentityPolicies.VerifiedMerchantSetup), RequestSizeLimit(8192)]
[ProducesResponseType<ProblemDetails>(400, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
public sealed class MerchantBranchController(ISender sender) : ControllerBase
{
    private Guid Actor => Guid.ParseExact(User.FindFirstValue("sub")!, "N");
    [HttpGet, ProducesResponseType<MerchantPage<BranchDto>>(200)]
    public async Task<IActionResult> List(Guid storeId, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default) =>
        (await sender.Send(new GetOwnBranchesQuery(Actor, storeId, page, pageSize), cancellationToken)).ToActionResult(HttpContext);
    [HttpPost, EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<BranchDto>(200)]
    public async Task<IActionResult> Create(Guid storeId, BranchWrite value, CancellationToken cancellationToken) =>
        (await sender.Send(new CreateBranchCommand(Actor, storeId, value), cancellationToken)).ToActionResult(HttpContext);
    [HttpPut("{id:guid}"), EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<BranchDto>(200)]
    public async Task<IActionResult> Edit(Guid storeId, Guid id, BranchUpdateWrite value, CancellationToken cancellationToken) =>
        (await sender.Send(new UpdateBranchCommand(Actor, storeId, id, value), cancellationToken)).ToActionResult(HttpContext);
}

[ApiController, Route(""), Tags("Public Branches and Pickup Points"), AllowAnonymous, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
public sealed class PublicBranchController(ISender sender) : ControllerBase
{
    [HttpGet("stores/{storeId:guid}/branches"), ProducesResponseType<MerchantPage<PublicBranchDto>>(200)]
    public async Task<IActionResult> List(Guid storeId, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default) =>
        (await sender.Send(new GetPublicBranchesQuery(storeId, page, pageSize), cancellationToken)).ToActionResult(HttpContext);
    [HttpGet("branches/{id:guid}"), ProducesResponseType<PublicBranchDto>(200)]
    public async Task<IActionResult> Read(Guid id, CancellationToken cancellationToken) =>
        (await sender.Send(new GetPublicBranchQuery(id), cancellationToken)).ToActionResult(HttpContext);
}
