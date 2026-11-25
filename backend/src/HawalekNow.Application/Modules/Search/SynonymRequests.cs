using System.Collections.Immutable;
using FluentValidation;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Search;
using MediatR;

namespace HawalekNow.Application.Modules.Search;

public sealed record SynonymWrite(string NameAr, string NameEn, bool IsActive, ImmutableArray<string> Terms, string? RowVersion = null);
public sealed record SynonymDelete(string RowVersion);
public sealed record SynonymDto(Guid Id, string NameAr, string NameEn, bool IsActive, ImmutableArray<string> Terms, string RowVersion);
public sealed record SynonymPage(ImmutableArray<SynonymDto> Items, int Page, int PageSize, bool HasMore);
public interface ISynonymAdministration
{
    Task<Result<SynonymDto>> SaveAsync(Guid? id, SynonymWrite value, CancellationToken cancellationToken);
    Task<Result> DeleteAsync(Guid id, string rowVersion, CancellationToken cancellationToken);
    Task<Result<SynonymPage>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
}
public sealed record SaveSynonymCommand(Guid? Id, SynonymWrite Value) : IRequest<Result<SynonymDto>>;
public sealed record DeleteSynonymCommand(Guid Id, string RowVersion) : IRequest<Result>;
public sealed record SynonymsQuery(int Page = 1, int PageSize = 30) : IRequest<Result<SynonymPage>>;
public sealed class SaveSynonymHandler(ISynonymAdministration service) : IRequestHandler<SaveSynonymCommand, Result<SynonymDto>>
{
    public Task<Result<SynonymDto>> Handle(SaveSynonymCommand request, CancellationToken cancellationToken) => service.SaveAsync(request.Id, request.Value, cancellationToken);
}
public sealed class DeleteSynonymHandler(ISynonymAdministration service) : IRequestHandler<DeleteSynonymCommand, Result>
{
    public Task<Result> Handle(DeleteSynonymCommand request, CancellationToken cancellationToken) => service.DeleteAsync(request.Id, request.RowVersion, cancellationToken);
}
public sealed class SynonymsHandler(ISynonymAdministration service) : IRequestHandler<SynonymsQuery, Result<SynonymPage>>
{
    public Task<Result<SynonymPage>> Handle(SynonymsQuery request, CancellationToken cancellationToken) => service.ListAsync(request.Page, request.PageSize, cancellationToken);
}
public static class SynonymInput
{
    public static bool Version(string? value) => value is not null && Convert.TryFromBase64String(value, new byte[8], out var size) && size == 8;
    public static bool Valid(SynonymWrite? value)
    {
        if (value is null || value.Terms.IsDefault) return false;
        try { _ = SynonymText.Name(value.NameAr); _ = SynonymText.Name(value.NameEn); _ = SynonymText.Keys(value.Terms); return true; }
        catch (ArgumentException) { return false; }
    }
}
public sealed class SaveSynonymValidator : AbstractValidator<SaveSynonymCommand>
{
    public SaveSynonymValidator() { RuleFor(value => value.Id).Must(value => value != Guid.Empty); RuleFor(value => value.Value).Must(SynonymInput.Valid); RuleFor(value => value.Value.RowVersion).Must(SynonymInput.Version).When(value => value.Id is not null && value.Value is not null); }
}
public sealed class DeleteSynonymValidator : AbstractValidator<DeleteSynonymCommand>
{
    public DeleteSynonymValidator() { RuleFor(value => value.Id).NotEmpty(); RuleFor(value => value.RowVersion).Must(SynonymInput.Version); }
}
