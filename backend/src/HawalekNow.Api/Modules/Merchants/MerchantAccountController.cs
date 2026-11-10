using System.Security.Claims;
using HawalekNow.Api.Shared.Errors;
using HawalekNow.Application.Modules.Identity.Contracts;
using HawalekNow.Application.Modules.Merchants.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HawalekNow.Api.Modules.Merchants;

public sealed record PublishingAccess(Guid MerchantAccountId, bool CanPublish);
[ApiController, Route("merchants/account"), Tags("Merchants")]
public sealed class MerchantAccountController(IMerchantAccounts accounts) : ControllerBase
{
    [HttpGet, Authorize(Policy = IdentityPolicies.VerifiedMerchantSetup)]
    [ProducesResponseType<MerchantAccountInfo>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> Account(CancellationToken cancellationToken)
    {
        var account = await accounts.FindByOwnerAsync(Guid.ParseExact(User.FindFirstValue("sub")!, "N"), cancellationToken);
        return account is null ? ApiProblems.AsObjectResult(ApiProblems.Create(HttpContext, 404)) : Ok(account);
    }
    [HttpGet("publishing-access"), Authorize(Policy = IdentityPolicies.ApprovedMerchant)]
    [ProducesResponseType<PublishingAccess>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> Publishing(CancellationToken cancellationToken)
    {
        var account = await accounts.FindByOwnerAsync(Guid.ParseExact(User.FindFirstValue("sub")!, "N"), cancellationToken);
        return account is null ? ApiProblems.AsObjectResult(ApiProblems.Create(HttpContext, 404)) : Ok(new PublishingAccess(account.Id, account.IsApproved));
    }
}
