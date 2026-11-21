using HawalekNow.Api.Shared.Errors;
using HawalekNow.Api.Shared.Security;
using HawalekNow.Application.Modules.Identity.Contracts;
using HawalekNow.Application.Modules.Locations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HawalekNow.Api.Modules.Locations;

[ApiController, Route("locations"), Tags("Locations"), AllowAnonymous]
[ProducesResponseType<LocationPage<LocationOption>>(200)]
[ProducesResponseType<ProblemDetails>(400, "application/problem+json")]
public sealed class LocationOptionsController(ISender sender) : ControllerBase
{
    [HttpGet("governorates")]
    public async Task<IActionResult> Governorates(string language = "ar", int page = 1, int pageSize = 30, CancellationToken cancellationToken = default) =>
        (await sender.Send(new LocationOptionsQuery(LocationKind.Governorate, Language: language, Page: page, PageSize: pageSize), cancellationToken)).ToActionResult(HttpContext);
    [HttpGet("cities")]
    public async Task<IActionResult> Cities(Guid governorateId, string language = "ar", int page = 1, int pageSize = 30, CancellationToken cancellationToken = default) =>
        (await sender.Send(new LocationOptionsQuery(LocationKind.City, governorateId, Language: language, Page: page, PageSize: pageSize), cancellationToken)).ToActionResult(HttpContext);
    [HttpGet("areas")]
    public async Task<IActionResult> Areas(Guid governorateId, Guid cityId, string language = "ar", int page = 1, int pageSize = 30, CancellationToken cancellationToken = default) =>
        (await sender.Send(new LocationOptionsQuery(LocationKind.Area, governorateId, cityId, language, page, pageSize), cancellationToken)).ToActionResult(HttpContext);
}

[ApiController, Route("admin/locations"), Tags("Locations Admin"), Authorize(Policy = IdentityPolicies.AdminOnly), RequestSizeLimit(8192)]
[ProducesResponseType<ProblemDetails>(400, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
public sealed class LocationAdminController(ISender sender) : ControllerBase
{
    [HttpGet("governorates"), ProducesResponseType<LocationPage<LocationAdminDto>>(200)]
    public async Task<IActionResult> Governorates(int page = 1, int pageSize = 30, CancellationToken cancellationToken = default) =>
        (await sender.Send(new AdminLocationsQuery(LocationKind.Governorate, Page: page, PageSize: pageSize), cancellationToken)).ToActionResult(HttpContext);
    [HttpGet("cities"), ProducesResponseType<LocationPage<LocationAdminDto>>(200)]
    public async Task<IActionResult> Cities(Guid governorateId, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default) =>
        (await sender.Send(new AdminLocationsQuery(LocationKind.City, governorateId, Page: page, PageSize: pageSize), cancellationToken)).ToActionResult(HttpContext);
    [HttpGet("areas"), ProducesResponseType<LocationPage<LocationAdminDto>>(200)]
    public async Task<IActionResult> Areas(Guid governorateId, Guid cityId, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default) =>
        (await sender.Send(new AdminLocationsQuery(LocationKind.Area, governorateId, cityId, page, pageSize), cancellationToken)).ToActionResult(HttpContext);

    [HttpPost("governorates"), EnableRateLimiting(ReferenceDataRateLimits.Mutation), ProducesResponseType<LocationAdminDto>(200)]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    public Task<IActionResult> CreateGovernorate(LocationWrite value, CancellationToken cancellationToken) => SaveAsync(LocationKind.Governorate, null, value, cancellationToken);
    [HttpPost("cities"), EnableRateLimiting(ReferenceDataRateLimits.Mutation), ProducesResponseType<LocationAdminDto>(200)]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    public Task<IActionResult> CreateCity(LocationWrite value, CancellationToken cancellationToken) => SaveAsync(LocationKind.City, null, value, cancellationToken);
    [HttpPost("areas"), EnableRateLimiting(ReferenceDataRateLimits.Mutation), ProducesResponseType<LocationAdminDto>(200)]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    public Task<IActionResult> CreateArea(LocationWrite value, CancellationToken cancellationToken) => SaveAsync(LocationKind.Area, null, value, cancellationToken);

    [HttpPut("governorates/{id:guid}"), EnableRateLimiting(ReferenceDataRateLimits.Mutation), ProducesResponseType<LocationAdminDto>(200)]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json"), ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    public Task<IActionResult> UpdateGovernorate(Guid id, LocationWrite value, CancellationToken cancellationToken) => SaveAsync(LocationKind.Governorate, id, value, cancellationToken);
    [HttpPut("cities/{id:guid}"), EnableRateLimiting(ReferenceDataRateLimits.Mutation), ProducesResponseType<LocationAdminDto>(200)]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json"), ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    public Task<IActionResult> UpdateCity(Guid id, LocationWrite value, CancellationToken cancellationToken) => SaveAsync(LocationKind.City, id, value, cancellationToken);
    [HttpPut("areas/{id:guid}"), EnableRateLimiting(ReferenceDataRateLimits.Mutation), ProducesResponseType<LocationAdminDto>(200)]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json"), ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    public Task<IActionResult> UpdateArea(Guid id, LocationWrite value, CancellationToken cancellationToken) => SaveAsync(LocationKind.Area, id, value, cancellationToken);

    [HttpDelete("governorates/{id:guid}"), EnableRateLimiting(ReferenceDataRateLimits.Mutation), ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json"), ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    public Task<IActionResult> DeleteGovernorate(Guid id, [FromBody] LocationDelete value, CancellationToken cancellationToken) => DeleteAsync(LocationKind.Governorate, id, value, cancellationToken);
    [HttpDelete("cities/{id:guid}"), EnableRateLimiting(ReferenceDataRateLimits.Mutation), ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json"), ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    public Task<IActionResult> DeleteCity(Guid id, [FromBody] LocationDelete value, CancellationToken cancellationToken) => DeleteAsync(LocationKind.City, id, value, cancellationToken);
    [HttpDelete("areas/{id:guid}"), EnableRateLimiting(ReferenceDataRateLimits.Mutation), ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json"), ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    public Task<IActionResult> DeleteArea(Guid id, [FromBody] LocationDelete value, CancellationToken cancellationToken) => DeleteAsync(LocationKind.Area, id, value, cancellationToken);

    private async Task<IActionResult> SaveAsync(LocationKind kind, Guid? id, LocationWrite value, CancellationToken cancellationToken) =>
        (await sender.Send(new SaveLocationCommand(kind, id, value), cancellationToken)).ToActionResult(HttpContext);
    private async Task<IActionResult> DeleteAsync(LocationKind kind, Guid id, LocationDelete value, CancellationToken cancellationToken) =>
        (await sender.Send(new DeleteLocationCommand(kind, id, value.RowVersion), cancellationToken)).ToActionResult(HttpContext);
}
