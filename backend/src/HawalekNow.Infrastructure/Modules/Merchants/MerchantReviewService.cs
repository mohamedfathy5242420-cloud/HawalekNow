using System.Collections.Immutable;
using HawalekNow.Application.Modules.Identity.Contracts;
using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Domain.Shared.Auditing;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Merchants;

public sealed class MerchantReviewService(HawalekDbContext context, IAccountAccess access, TimeProvider clock) : IMerchantReviewService
{
    public async Task<Result<MerchantReviewDto>> ReviewAsync(Guid actorId, Guid id, MerchantReviewAction action, MerchantReviewWrite value, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || !MerchantReviewInput.Write(value, action)) return Failure<MerchantReviewDto>(ErrorKind.Validation);
        try
        {
            await using var transaction = await context.BeginAsync(cancellationToken);
            if (!Admin(await access.FindForMutationAsync(actorId, cancellationToken))) return Failure<MerchantReviewDto>(ErrorKind.Forbidden);
            var owner = await context.Set<MerchantAccount>().AsNoTracking().Where(item => item.Id == id).Select(item => (Guid?)item.UserId).SingleOrDefaultAsync(cancellationToken);
            if (owner is null) return Failure<MerchantReviewDto>(ErrorKind.NotFound);
            // Same Identity state lock used by deletion/recovery, held until this outer transaction ends.
            var identity = await access.FindForMutationAsync(owner.Value, cancellationToken);
            if (!Merchant(identity)) return Failure<MerchantReviewDto>(ErrorKind.Conflict);
            var account = await context.Set<MerchantAccount>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (account is null) return Failure<MerchantReviewDto>(ErrorKind.NotFound);
            await context.Entry(account).ReloadAsync(cancellationToken);
            if (!Matches(account.RowVersion, value.RowVersion)) return Failure<MerchantReviewDto>(ErrorKind.Conflict);
            var before = account.Status;
            var changed = action switch
            {
                MerchantReviewAction.Approve => account.TryReview(MerchantApprovalStatus.Approved),
                MerchantReviewAction.RequestChanges => account.TryReview(MerchantApprovalStatus.ChangesRequested),
                MerchantReviewAction.Reject => account.TryReview(MerchantApprovalStatus.Rejected),
                MerchantReviewAction.Suspend => account.TrySuspend(),
                MerchantReviewAction.Reinstate => account.TryReinstate(),
                _ => false
            };
            if (!changed) return Failure<MerchantReviewDto>(ErrorKind.Conflict);
            var now = clock.GetUtcNow();
            account.RecordReview(actorId, value.Reason, now);
            context.Add(AuditEntry.Record(actorId, "Merchant." + action, "MerchantAccount", account.Id, value.Reason, now, before.ToString(), account.Status.ToString()));
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<MerchantReviewDto>.Success(ToDto(account, identity));
        }
        catch (DbUpdateException) { context.ChangeTracker.Clear(); return Failure<MerchantReviewDto>(ErrorKind.Conflict); }
        catch (SqlException error) when (error.Number is 1205 or 1222 or 51003) { context.ChangeTracker.Clear(); return Failure<MerchantReviewDto>(ErrorKind.Conflict); }
    }

    public async Task<Result<MerchantReviewDto>> ResubmitAsync(Guid actorId, string rowVersion, CancellationToken cancellationToken)
    {
        if (!MerchantReviewInput.Version(rowVersion)) return Failure<MerchantReviewDto>(ErrorKind.Validation);
        try
        {
            await using var transaction = await context.BeginAsync(cancellationToken);
            var identity = await access.FindForMutationAsync(actorId, cancellationToken);
            if (!Merchant(identity)) return Failure<MerchantReviewDto>(ErrorKind.Forbidden);
            var account = await context.Set<MerchantAccount>().SingleOrDefaultAsync(item => item.UserId == actorId, cancellationToken);
            if (account is null) return Failure<MerchantReviewDto>(ErrorKind.NotFound);
            await context.Entry(account).ReloadAsync(cancellationToken);
            if (!Matches(account.RowVersion, rowVersion)) return Failure<MerchantReviewDto>(ErrorKind.Conflict);
            var before = account.Status;
            if (!account.TryResubmit()) return Failure<MerchantReviewDto>(ErrorKind.Conflict);
            context.Add(AuditEntry.Record(actorId, "Merchant.Resubmit", "MerchantAccount", account.Id, null, clock.GetUtcNow(), before.ToString(), account.Status.ToString()));
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<MerchantReviewDto>.Success(ToDto(account, identity));
        }
        catch (DbUpdateException) { context.ChangeTracker.Clear(); return Failure<MerchantReviewDto>(ErrorKind.Conflict); }
        catch (SqlException error) when (error.Number is 1205 or 1222 or 51003) { context.ChangeTracker.Clear(); return Failure<MerchantReviewDto>(ErrorKind.Conflict); }
    }

    public async Task<Result<MerchantReviewDto>> OwnAsync(Guid actorId, CancellationToken cancellationToken)
    {
        var identity = await access.FindAsync(actorId, cancellationToken);
        if (!Merchant(identity)) return Failure<MerchantReviewDto>(ErrorKind.Forbidden);
        var account = await context.Set<MerchantAccount>().AsNoTracking().SingleOrDefaultAsync(item => item.UserId == actorId, cancellationToken);
        if (account is null) return Failure<MerchantReviewDto>(ErrorKind.NotFound);
        if (account.Status == MerchantApprovalStatus.Suspended) return Failure<MerchantReviewDto>(ErrorKind.Forbidden);
        return Result<MerchantReviewDto>.Success(ToDto(account, identity));
    }

    public async Task<Result<MerchantPage<MerchantReviewDto>>> ListAsync(Guid actorId, string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (!Admin(await access.FindAsync(actorId, cancellationToken))) return Failure<MerchantPage<MerchantReviewDto>>(ErrorKind.Forbidden);
        if (!MerchantReviewInput.Page(page, pageSize) || !MerchantReviewInput.Status(status)) return Failure<MerchantPage<MerchantReviewDto>>(ErrorKind.Validation);
        var query = context.Set<MerchantAccount>().AsNoTracking();
        if (status is not null) { var selected = Enum.Parse<MerchantApprovalStatus>(status, true); query = query.Where(item => item.Status == selected); }
        var rows = await query.OrderBy(item => item.Id).Skip((page - 1) * pageSize).Take(pageSize + 1).ToListAsync(cancellationToken);
        var items = ImmutableArray.CreateBuilder<MerchantReviewDto>();
        foreach (var row in rows.Take(pageSize)) items.Add(ToDto(row, await access.FindAsync(row.UserId, cancellationToken)));
        return Result<MerchantPage<MerchantReviewDto>>.Success(new(items.ToImmutable(), page, pageSize, rows.Count > pageSize));
    }

    public async Task<Result<MerchantPage<MerchantAuditDto>>> AuditAsync(Guid actorId, Guid id, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (!Admin(await access.FindAsync(actorId, cancellationToken))) return Failure<MerchantPage<MerchantAuditDto>>(ErrorKind.Forbidden);
        if (id == Guid.Empty || !MerchantReviewInput.Page(page, pageSize)) return Failure<MerchantPage<MerchantAuditDto>>(ErrorKind.Validation);
        var rows = await context.Set<AuditEntry>().AsNoTracking().Where(item => item.TargetKind == "MerchantAccount" && item.TargetId == id)
            .OrderByDescending(item => item.OccurredAt).ThenBy(item => item.Id).Skip((page - 1) * pageSize).Take(pageSize + 1)
            .Select(item => new MerchantAuditDto(item.Id, item.ActorId, item.Operation, item.TargetId, item.Reason, item.OccurredAt, item.PreviousState, item.CurrentState)).ToListAsync(cancellationToken);
        return Result<MerchantPage<MerchantAuditDto>>.Success(new(rows.Take(pageSize).ToImmutableArray(), page, pageSize, rows.Count > pageSize));
    }

    private static bool Admin(AccountAccessInfo? value) => value is { AccountType: "Admin", EmailConfirmed: true, IsActive: true };
    private static bool Merchant(AccountAccessInfo? value) => value is { AccountType: "Merchant", EmailConfirmed: true, IsActive: true };
    private static bool Matches(byte[] actual, string expected) => actual.AsSpan().SequenceEqual(Convert.FromBase64String(expected));
    private static Result<T> Failure<T>(ErrorKind kind) => Result<T>.Failure(new(kind));
    private static MerchantReviewDto ToDto(MerchantAccount account, AccountAccessInfo? identity) => new(account.Id, account.UserId,
        identity?.UserName, identity?.Email, identity?.EmailConfirmed ?? false, account.Status.ToString(), account.ReviewReason, account.ReviewedAt, Convert.ToBase64String(account.RowVersion));
}
