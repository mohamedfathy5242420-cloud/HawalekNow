using HawalekNow.Api.Shared.Errors;
using HawalekNow.Api.Shared.Security;
using HawalekNow.Application.Modules.Catalog;
using HawalekNow.Application.Modules.Identity.Contracts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HawalekNow.Api.Modules.Catalog;

[ApiController, Route("catalog"), Tags("Catalog")]
[ProducesResponseType<ProblemDetails>(400, "application/problem+json")]
public sealed class CatalogOptionsController(ISender sender) : ControllerBase
{
    [HttpGet("activities"), AllowAnonymous]
    [ProducesResponseType<CatalogPage<CatalogOption>>(200)]
    public async Task<IActionResult> Activities(string language = "ar", int page = 1, int pageSize = 30, CancellationToken cancellationToken = default) =>
        (await sender.Send(new ListActivitiesQuery(language, page, pageSize), cancellationToken)).ToActionResult(HttpContext);
    [HttpGet("categories"), AllowAnonymous]
    [ProducesResponseType<CatalogPage<CatalogOption>>(200)]
    public async Task<IActionResult> Categories(Guid activityId, Guid? parentId = null, string language = "ar", int page = 1, int pageSize = 30, CancellationToken cancellationToken = default) =>
        (await sender.Send(new ListCategoriesQuery(activityId, parentId, language, page, pageSize), cancellationToken)).ToActionResult(HttpContext);
}

[ApiController, Route("admin/catalog"), Tags("Catalog Admin"), Authorize(Policy = IdentityPolicies.AdminOnly), RequestSizeLimit(8192)]
[ProducesResponseType<ProblemDetails>(400, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
public sealed class CatalogAdminController(ISender sender) : ControllerBase
{
    [HttpGet("activities")]
    [ProducesResponseType<CatalogPage<ActivityAdminDto>>(200)]
    public async Task<IActionResult> Activities(int page = 1, int pageSize = 30, CancellationToken cancellationToken = default) =>
        (await sender.Send(new ListAdminActivitiesQuery(page, pageSize), cancellationToken)).ToActionResult(HttpContext);
    [HttpGet("categories")]
    [ProducesResponseType<CatalogPage<CategoryAdminDto>>(200)]
    public async Task<IActionResult> Categories(Guid activityId, Guid? parentId = null, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default) =>
        (await sender.Send(new ListAdminCategoriesQuery(activityId, parentId, page, pageSize), cancellationToken)).ToActionResult(HttpContext);
    [HttpPost("activities"), EnableRateLimiting(ReferenceDataRateLimits.Mutation)]
    [ProducesResponseType<ActivityAdminDto>(200)]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    public async Task<IActionResult> CreateActivity(ActivityWrite value, CancellationToken cancellationToken) =>
        (await sender.Send(new SaveActivityCommand(null, value), cancellationToken)).ToActionResult(HttpContext);
    [HttpPut("activities/{id:guid}"), EnableRateLimiting(ReferenceDataRateLimits.Mutation)]
    [ProducesResponseType<ActivityAdminDto>(200)]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    public async Task<IActionResult> UpdateActivity(Guid id, ActivityWrite value, CancellationToken cancellationToken) =>
        (await sender.Send(new SaveActivityCommand(id, value), cancellationToken)).ToActionResult(HttpContext);
    [HttpDelete("activities/{id:guid}"), EnableRateLimiting(ReferenceDataRateLimits.Mutation)]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    public async Task<IActionResult> DeleteActivity(Guid id, [FromBody] DeleteReference value, CancellationToken cancellationToken) =>
        (await sender.Send(new DeleteActivityCommand(id, value.RowVersion), cancellationToken)).ToActionResult(HttpContext);
    [HttpPost("categories"), EnableRateLimiting(ReferenceDataRateLimits.Mutation)]
    [ProducesResponseType<CategoryAdminDto>(200)]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    public async Task<IActionResult> CreateCategory(CategoryWrite value, CancellationToken cancellationToken) =>
        (await sender.Send(new SaveCategoryCommand(null, value), cancellationToken)).ToActionResult(HttpContext);
    [HttpPut("categories/{id:guid}"), EnableRateLimiting(ReferenceDataRateLimits.Mutation)]
    [ProducesResponseType<CategoryAdminDto>(200)]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    public async Task<IActionResult> UpdateCategory(Guid id, CategoryWrite value, CancellationToken cancellationToken) =>
        (await sender.Send(new SaveCategoryCommand(id, value), cancellationToken)).ToActionResult(HttpContext);
    [HttpDelete("categories/{id:guid}"), EnableRateLimiting(ReferenceDataRateLimits.Mutation)]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    public async Task<IActionResult> DeleteCategory(Guid id, [FromBody] DeleteReference value, CancellationToken cancellationToken) =>
        (await sender.Send(new DeleteCategoryCommand(id, value.RowVersion), cancellationToken)).ToActionResult(HttpContext);
}
