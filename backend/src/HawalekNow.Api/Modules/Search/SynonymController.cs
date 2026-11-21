using HawalekNow.Api.Shared.Errors;
using HawalekNow.Api.Shared.Security;
using HawalekNow.Application.Modules.Identity.Contracts;
using HawalekNow.Application.Modules.Search;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HawalekNow.Api.Modules.Search;

[ApiController, Route("admin/search/synonym-groups"), Tags("Search Admin"), Authorize(Policy = IdentityPolicies.AdminOnly), RequestSizeLimit(32768)]
[ProducesResponseType<ProblemDetails>(400, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
public sealed class SynonymController(ISender sender) : ControllerBase
{
    [HttpGet, ProducesResponseType<SynonymPage>(200)]
    public async Task<IActionResult> List(int page = 1, int pageSize = 30, CancellationToken cancellationToken = default) => (await sender.Send(new SynonymsQuery(page, pageSize), cancellationToken)).ToActionResult(HttpContext);
    [HttpPost, EnableRateLimiting(ReferenceDataRateLimits.Mutation), ProducesResponseType<SynonymDto>(200)]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    public async Task<IActionResult> Create(SynonymWrite value, CancellationToken cancellationToken) => (await sender.Send(new SaveSynonymCommand(null, value), cancellationToken)).ToActionResult(HttpContext);
    [HttpPut("{id:guid}"), EnableRateLimiting(ReferenceDataRateLimits.Mutation), ProducesResponseType<SynonymDto>(200)]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json"), ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    public async Task<IActionResult> Update(Guid id, SynonymWrite value, CancellationToken cancellationToken) => (await sender.Send(new SaveSynonymCommand(id, value), cancellationToken)).ToActionResult(HttpContext);
    [HttpDelete("{id:guid}"), EnableRateLimiting(ReferenceDataRateLimits.Mutation), ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json"), ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    public async Task<IActionResult> Delete(Guid id, SynonymDelete value, CancellationToken cancellationToken) => (await sender.Send(new DeleteSynonymCommand(id, value.RowVersion), cancellationToken)).ToActionResult(HttpContext);
}
