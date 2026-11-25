namespace HawalekNow.Domain.Modules.Merchants;

public enum MerchantApprovalStatus { Pending = 1, ChangesRequested = 2, Approved = 3, Rejected = 4, Suspended = 5 }

public sealed class MerchantAccount
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public MerchantApprovalStatus Status { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public bool CanPrepareAccount => Status is MerchantApprovalStatus.Pending or MerchantApprovalStatus.ChangesRequested or MerchantApprovalStatus.Approved;
    public bool CanPublish => Status == MerchantApprovalStatus.Approved;

    private MerchantAccount() { }

    public static MerchantAccount CreatePending(Guid userId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("An account owner is required.", nameof(userId));
        return new() { Id = Guid.CreateVersion7(), UserId = userId, Status = MerchantApprovalStatus.Pending };
    }

    // Email confirmation belongs to Identity and never invokes these decisions.
    public bool TryReview(MerchantApprovalStatus decision)
    {
        if (decision is not (MerchantApprovalStatus.Approved or MerchantApprovalStatus.ChangesRequested or MerchantApprovalStatus.Rejected))
            throw new ArgumentOutOfRangeException(nameof(decision));
        if (Status != MerchantApprovalStatus.Pending) return false;
        Status = decision;
        return true;
    }

    public bool TryResubmit()
    {
        if (Status != MerchantApprovalStatus.ChangesRequested) return false;
        Status = MerchantApprovalStatus.Pending;
        return true;
    }

    public bool TrySuspend()
    {
        if (Status != MerchantApprovalStatus.Approved) return false;
        Status = MerchantApprovalStatus.Suspended;
        return true;
    }

    public bool TryReinstate()
    {
        if (Status != MerchantApprovalStatus.Suspended) return false;
        Status = MerchantApprovalStatus.Approved;
        return true;
    }
}
