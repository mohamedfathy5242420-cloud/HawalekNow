using System.Collections.Immutable;
using HawalekNow.Application.Modules.Search;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Search;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.Infrastructure.Modules.Search;

public sealed class SynonymGroupConfiguration : IEntityTypeConfiguration<SynonymGroup>
{
    public void Configure(EntityTypeBuilder<SynonymGroup> builder)
    {
        builder.ToTable("SynonymGroups", "search", table => table.HasCheckConstraint("CK_SynonymGroups_Names", "LEN([NameAr]) > 0 AND LEN([NameEn]) > 0")); builder.HasKey(value => value.Id);
        builder.Property(value => value.NameAr).HasMaxLength(100).IsRequired(); builder.Property(value => value.NameEn).HasMaxLength(100).IsRequired(); builder.Property(value => value.RowVersion).IsRowVersion();
    }
}
public sealed class SynonymTermConfiguration : IEntityTypeConfiguration<SynonymTerm>
{
    public void Configure(EntityTypeBuilder<SynonymTerm> builder)
    {
        builder.ToTable("SynonymTerms", "search", table => table.HasCheckConstraint("CK_SynonymTerms_Text", "LEN([Original]) > 0 AND LEN([NormalizedKey]) > 0")); builder.HasKey(value => value.Id);
        builder.Property(value => value.Original).HasMaxLength(80).IsRequired(); builder.Property(value => value.NormalizedKey).HasMaxLength(256).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.HasIndex(value => value.NormalizedKey).IsUnique(); builder.HasOne<SynonymGroup>().WithMany().HasForeignKey(value => value.GroupId).OnDelete(DeleteBehavior.Cascade);
    }
}
public static class SearchRegistration
{
    public static IServiceCollection AddSearchReferenceData(this IServiceCollection services) => services.AddScoped<ISynonymAdministration, SynonymAdministration>();
}
public sealed class SynonymAdministration(HawalekDbContext context) : ISynonymAdministration
{
    private Task LockAsync(bool readOnly, CancellationToken ct) => context.Database.ExecuteSqlInterpolatedAsync($"""
        DECLARE @result int;
        EXEC @result = sys.sp_getapplock @Resource={"search-synonym-membership"}, @LockMode={(readOnly ? "Shared" : "Exclusive")}, @LockOwner=N'Transaction', @LockTimeout=10000;
        IF @result < 0 THROW 51012, N'Reference membership lock unavailable.', 1;
        """, ct);
    public async Task<Result<SynonymDto>> SaveAsync(Guid? id, SynonymWrite value, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || !SynonymInput.Valid(value) || (id is not null && !SynonymInput.Version(value.RowVersion))) return Failure(ErrorKind.Validation);
        var keys = SynonymText.Keys(value.Terms);
        try
        {
            await using var transaction = await context.BeginAsync(cancellationToken); await LockAsync(false, cancellationToken);
            var group = id is null ? SynonymGroup.Create(value.NameAr, value.NameEn, value.IsActive) : await context.Set<SynonymGroup>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (group is null) return Failure(ErrorKind.NotFound);
            if (id is not null)
            {
                await context.Entry(group).ReloadAsync(cancellationToken);
                if (!group.RowVersion.AsSpan().SequenceEqual(Convert.FromBase64String(value.RowVersion!))) return Failure(ErrorKind.Conflict);
            }
            if (await context.Set<SynonymTerm>().AnyAsync(item => item.GroupId != group.Id && keys.Contains(item.NormalizedKey), cancellationToken)) return Failure(ErrorKind.Conflict);
            if (id is null) context.Add(group);
            else
            {
                group.Edit(value.NameAr, value.NameEn, value.IsActive);
                // Terms-only edits must also advance the aggregate concurrency token.
                context.Entry(group).Property(item => item.IsActive).IsModified = true;
                var old = await context.Set<SynonymTerm>().Where(item => item.GroupId == group.Id).ToArrayAsync(cancellationToken); context.RemoveRange(old);
            }
            await context.SaveChangesAsync(cancellationToken);
            foreach (var original in value.Terms) context.Add(SynonymTerm.Create(group.Id, original));
            await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
            return Result<SynonymDto>.Success(ToDto(group, value.Terms));
        }
        catch (DbUpdateException) { context.ChangeTracker.Clear(); return Failure(ErrorKind.Conflict); }
        catch (SqlException error) when (error.Number is 1205 or 1222 or 51012) { context.ChangeTracker.Clear(); return Failure(ErrorKind.Conflict); }
    }
    public async Task<Result> DeleteAsync(Guid id, string rowVersion, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || !SynonymInput.Version(rowVersion)) return Result.Failure(new(ErrorKind.Validation));
        try
        {
            await using var transaction = await context.BeginAsync(cancellationToken); await LockAsync(false, cancellationToken);
            var group = await context.Set<SynonymGroup>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (group is null) return Result.Failure(new(ErrorKind.NotFound)); await context.Entry(group).ReloadAsync(cancellationToken);
            if (!group.RowVersion.AsSpan().SequenceEqual(Convert.FromBase64String(rowVersion))) return Result.Failure(new(ErrorKind.Conflict));
            context.Remove(group); await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return Result.Success();
        }
        catch (DbUpdateException) { context.ChangeTracker.Clear(); return Result.Failure(new(ErrorKind.Conflict)); }
        catch (SqlException error) when (error.Number is 1205 or 1222 or 51012) { context.ChangeTracker.Clear(); return Result.Failure(new(ErrorKind.Conflict)); }
    }
    public async Task<Result<SynonymPage>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page is < 1 or > 100_000 || pageSize is < 1 or > 100) return Result<SynonymPage>.Failure(new(ErrorKind.Validation));
        await using var transaction = await context.BeginAsync(cancellationToken); await LockAsync(true, cancellationToken);
        var groups = await context.Set<SynonymGroup>().AsNoTracking().OrderBy(item => item.Id).Skip((page - 1) * pageSize).Take(pageSize + 1).ToArrayAsync(cancellationToken);
        var ids = groups.Take(pageSize).Select(item => item.Id).ToArray();
        var terms = await context.Set<SynonymTerm>().AsNoTracking().Where(item => ids.Contains(item.GroupId)).OrderBy(item => item.NormalizedKey).ToArrayAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<SynonymPage>.Success(new(groups.Take(pageSize).Select(item => ToDto(item, terms.Where(term => term.GroupId == item.Id).Select(term => term.Original).ToImmutableArray())).ToImmutableArray(), page, pageSize, groups.Length > pageSize));
    }
    private static SynonymDto ToDto(SynonymGroup group, ImmutableArray<string> terms) => new(group.Id, group.NameAr, group.NameEn, group.IsActive, terms, Convert.ToBase64String(group.RowVersion));
    private static Result<SynonymDto> Failure(ErrorKind kind) => Result<SynonymDto>.Failure(new(kind));
}
