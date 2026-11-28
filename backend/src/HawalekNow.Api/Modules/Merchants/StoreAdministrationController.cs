using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HawalekNow.Api.Shared.Errors;
using HawalekNow.Api.Shared.Security;
using HawalekNow.Application.Modules.Identity.Contracts;
using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Application.Shared.Storage;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HawalekNow.Api.Modules.Merchants;

public sealed class AdminStoreLogoForm
{
    [Required] public string RowVersion { get; set; } = "";
    [Required] public IFormFile? File { get; set; }
    [Required] public string Reason { get; set; } = "";
}
[ApiController, Route("admin"), Tags("Administrative Stores"), Authorize(Policy = IdentityPolicies.AdminOnly), RequestSizeLimit(8192)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(503, "application/problem+json")]
public sealed class StoreAdministrationController(ISender sender) : ControllerBase
{
    private Guid Actor => Guid.ParseExact(User.FindFirstValue("sub")!, "N");
    [HttpGet("stores"), ProducesResponseType<MerchantPage<AdminStoreDto>>(200)]
    public async Task<IActionResult> List(int page = 1, int pageSize = 25, CancellationToken cancellationToken = default) =>
        (await sender.Send(new ListAdminStoresQuery(Actor, page, pageSize), cancellationToken)).ToActionResult(HttpContext);
    [HttpGet("stores/{id:guid}"), ProducesResponseType<AdminStoreDto>(200)]
    public async Task<IActionResult> Read(Guid id, CancellationToken cancellationToken) =>
        (await sender.Send(new ReadAdminStoreQuery(Actor, id), cancellationToken)).ToActionResult(HttpContext);
    [HttpPut("stores/{id:guid}"), EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<StoreDto>(200)]
    public async Task<IActionResult> Edit(Guid id, AdminStoreUpdateWrite value, CancellationToken cancellationToken) =>
        (await sender.Send(new UpdateAdminStoreCommand(Actor, id, value), cancellationToken)).ToActionResult(HttpContext);
    [HttpPost("stores/{id:guid}/hide"), EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<StoreDto>(200)]
    public Task<IActionResult> Hide(Guid id, StoreModerationWrite value, CancellationToken cancellationToken) => Moderate(id, StoreAdministrationAction.Hide, value, cancellationToken);
    [HttpPost("stores/{id:guid}/publish"), EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<StoreDto>(200)]
    public Task<IActionResult> Publish(Guid id, StoreModerationWrite value, CancellationToken cancellationToken) => Moderate(id, StoreAdministrationAction.Publish, value, cancellationToken);
    [HttpPost("stores/{id:guid}/suspend"), EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<StoreDto>(200)]
    public Task<IActionResult> Suspend(Guid id, StoreModerationWrite value, CancellationToken cancellationToken) => Moderate(id, StoreAdministrationAction.Suspend, value, cancellationToken);
    [HttpPost("stores/{id:guid}/resume"), EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<StoreDto>(200)]
    public Task<IActionResult> Resume(Guid id, StoreModerationWrite value, CancellationToken cancellationToken) => Moderate(id, StoreAdministrationAction.Resume, value, cancellationToken);
    private async Task<IActionResult> Moderate(Guid id, StoreAdministrationAction action, StoreModerationWrite value, CancellationToken cancellationToken) =>
        (await sender.Send(new ModerateStoreCommand(Actor, id, action, value), cancellationToken)).ToActionResult(HttpContext);
    [HttpGet("stores/{id:guid}/logo"), ProducesResponseType<FileContentResult>(200, "image/png")]
    public async Task<IActionResult> Logo(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ReadAdminStoreLogoQuery(Actor, id), cancellationToken);
        if (!result.IsSuccess) return result.ToActionResult(HttpContext);
        Response.Headers.XContentTypeOptions = "nosniff"; return File(result.Value, "image/png");
    }
    [HttpPut("stores/{id:guid}/logo"), Consumes("multipart/form-data"), EnableRateLimiting(MerchantRateLimits.Mutation)]
    [RequestSizeLimit(ImageStorageLimits.MaxInputBytes + 8192), RequestFormLimits(MultipartBodyLengthLimit = ImageStorageLimits.MaxInputBytes + 8192)]
    [ProducesResponseType<StoreDto>(200), ProducesResponseType<ProblemDetails>(413, "application/problem+json")]
    public async Task<IActionResult> Upload(Guid id, [FromForm] AdminStoreLogoForm value, CancellationToken cancellationToken)
    {
        if (value.File is null || value.File.Length is <= 0 or > ImageStorageLimits.MaxInputBytes) return ApiProblems.AsObjectResult(ApiProblems.Create(HttpContext, 400));
        await using var stream = value.File.OpenReadStream();
        return (await sender.Send(new ChangeAdminStoreLogoCommand(Actor, id, value.RowVersion, stream, value.Reason), cancellationToken)).ToActionResult(HttpContext);
    }
    [HttpDelete("stores/{id:guid}/logo"), EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<StoreDto>(200)]
    public async Task<IActionResult> RemoveLogo(Guid id, StoreModerationWrite value, CancellationToken cancellationToken) =>
        (await sender.Send(new ChangeAdminStoreLogoCommand(Actor, id, value.RowVersion, null, value.Reason), cancellationToken)).ToActionResult(HttpContext);
    [HttpGet("stores/{storeId:guid}/branches"), ProducesResponseType<MerchantPage<BranchDto>>(200)]
    public async Task<IActionResult> Branches(Guid storeId, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default) =>
        (await sender.Send(new ListAdminBranchesQuery(Actor, storeId, page, pageSize), cancellationToken)).ToActionResult(HttpContext);
    [HttpPut("stores/{storeId:guid}/branches/{id:guid}"), EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<BranchDto>(200)]
    public async Task<IActionResult> EditBranch(Guid storeId, Guid id, AdminBranchUpdateWrite value, CancellationToken cancellationToken) =>
        (await sender.Send(new UpdateAdminBranchCommand(Actor, storeId, id, value), cancellationToken)).ToActionResult(HttpContext);
    [HttpGet("stores/{id:guid}/audit"), ProducesResponseType<MerchantPage<MerchantAuditDto>>(200)]
    public async Task<IActionResult> Audit(Guid id, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default) =>
        (await sender.Send(new ReadStoreAuditQuery(Actor, id, false, page, pageSize), cancellationToken)).ToActionResult(HttpContext);
    [HttpGet("merchant-branches/{id:guid}/audit"), ProducesResponseType<MerchantPage<MerchantAuditDto>>(200)]
    public async Task<IActionResult> BranchAudit(Guid id, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default) =>
        (await sender.Send(new ReadStoreAuditQuery(Actor, id, true, page, pageSize), cancellationToken)).ToActionResult(HttpContext);
}
