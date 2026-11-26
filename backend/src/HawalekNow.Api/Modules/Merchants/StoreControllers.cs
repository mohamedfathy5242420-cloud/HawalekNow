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

public sealed class StoreLogoForm
{
    [Required] public string RowVersion { get; set; } = "";
    [Required] public IFormFile? File { get; set; }
}
public sealed record StoreLogoRemoveWrite(string RowVersion);

[ApiController, Route("merchants"), Tags("Merchant Store"), Authorize(Policy = IdentityPolicies.VerifiedMerchantSetup), RequestSizeLimit(8192)]
[ProducesResponseType<ProblemDetails>(400, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(503, "application/problem+json")]
public sealed class MerchantStoreController(ISender sender) : ControllerBase
{
    private Guid Actor => Guid.ParseExact(User.FindFirstValue("sub")!, "N");
    [HttpGet("store"), ProducesResponseType<StoreDto>(200)]
    public async Task<IActionResult> Own(CancellationToken cancellationToken) => (await sender.Send(new GetOwnStoreQuery(Actor), cancellationToken)).ToActionResult(HttpContext);
    [HttpPost("store"), EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<StoreDto>(200)]
    public async Task<IActionResult> Create(StoreCreateWrite value, CancellationToken cancellationToken) => (await sender.Send(new CreateStoreCommand(Actor, value), cancellationToken)).ToActionResult(HttpContext);
    [HttpPut("stores/{id:guid}"), EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<StoreDto>(200)]
    public async Task<IActionResult> Update(Guid id, StoreUpdateWrite value, CancellationToken cancellationToken) => (await sender.Send(new UpdateStoreCommand(Actor, id, value), cancellationToken)).ToActionResult(HttpContext);
    [HttpPut("stores/{id:guid}/logo"), Consumes("multipart/form-data"), EnableRateLimiting(MerchantRateLimits.Mutation)]
    [RequestSizeLimit(ImageStorageLimits.MaxInputBytes + 8192), RequestFormLimits(MultipartBodyLengthLimit = ImageStorageLimits.MaxInputBytes + 8192)]
    [ProducesResponseType<StoreDto>(200), ProducesResponseType<ProblemDetails>(413, "application/problem+json")]
    public async Task<IActionResult> Upload(Guid id, [FromForm] StoreLogoForm value, CancellationToken cancellationToken)
    {
        if (value.File is null || value.File.Length is <= 0 or > ImageStorageLimits.MaxInputBytes) return ApiProblems.AsObjectResult(ApiProblems.Create(HttpContext, 400));
        await using var stream = value.File.OpenReadStream();
        return (await sender.Send(new ChangeStoreLogoCommand(Actor, id, value.RowVersion, stream), cancellationToken)).ToActionResult(HttpContext);
    }
    [HttpDelete("stores/{id:guid}/logo"), EnableRateLimiting(MerchantRateLimits.Mutation), ProducesResponseType<StoreDto>(200)]
    public async Task<IActionResult> RemoveLogo(Guid id, StoreLogoRemoveWrite value, CancellationToken cancellationToken) =>
        (await sender.Send(new ChangeStoreLogoCommand(Actor, id, value.RowVersion, null), cancellationToken)).ToActionResult(HttpContext);
    [HttpGet("stores/{id:guid}/logo"), ProducesResponseType<FileContentResult>(200, "image/png")]
    public async Task<IActionResult> Logo(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetStoreLogoQuery(Actor, id), cancellationToken);
        if (!result.IsSuccess) return result.ToActionResult(HttpContext);
        Response.Headers.CacheControl = "no-store"; Response.Headers.XContentTypeOptions = "nosniff"; return File(result.Value, "image/png");
    }
}

[ApiController, Route("stores"), Tags("Public Stores"), AllowAnonymous, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(503, "application/problem+json")]
public sealed class PublicStoreController(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}"), ProducesResponseType<PublicStoreDto>(200)]
    public async Task<IActionResult> Read(Guid id, CancellationToken cancellationToken) => (await sender.Send(new GetPublicStoreQuery(id), cancellationToken)).ToActionResult(HttpContext);
    [HttpGet("{id:guid}/logo"), ProducesResponseType<FileContentResult>(200, "image/png")]
    public async Task<IActionResult> Logo(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetStoreLogoQuery(null, id), cancellationToken);
        if (!result.IsSuccess) return result.ToActionResult(HttpContext);
        Response.Headers.XContentTypeOptions = "nosniff"; return File(result.Value, "image/png");
    }
}
