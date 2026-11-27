using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Domain.Modules.Merchants;

namespace HawalekNow.UnitTests;

public sealed class ActivityChangeTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-26T12:00:00Z");
    private static StoreActivityChange Request() => StoreActivityChange.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), " owner context ", Now);
    [Fact]
    public void A_request_starts_pending_and_preserves_immutable_proposal_and_attribution()
    {
        var change = Request(); Assert.Equal(ActivityChangeStatus.Pending, change.Status); Assert.NotEqual(change.PreviousActivityId, change.RequestedActivityId);
        Assert.NotEqual(Guid.Empty, change.StoreId); Assert.NotEqual(Guid.Empty, change.RequestedBy); Assert.Equal(Now, change.RequestedAt); Assert.Equal("owner context", change.RequestReason);
        Assert.Null(change.ReviewedBy); Assert.Null(change.ReviewedAt); Assert.Null(change.ReviewReason);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Only_one_decision_can_be_recorded(bool approve)
    {
        var change = Request(); var admin = Guid.NewGuid();
        Assert.True(change.TryReview(approve, admin, " review context ", Now.AddMinutes(1)));
        Assert.Equal(approve ? ActivityChangeStatus.Approved : ActivityChangeStatus.Rejected, change.Status); Assert.Equal(admin, change.ReviewedBy);
        Assert.Equal("review context", change.ReviewReason); Assert.Equal(Now.AddMinutes(1), change.ReviewedAt);
        Assert.False(change.TryReview(!approve, Guid.NewGuid(), "another decision", Now.AddMinutes(2))); Assert.Equal(admin, change.ReviewedBy);
    }
    [Fact]
    public void Reject_needs_a_reason_and_invalid_decisions_leave_the_request_untouched()
    {
        var change = Request();
        Assert.False(change.TryReview(false, Guid.NewGuid(), " ", Now)); Assert.False(change.TryReview(true, Guid.Empty, null, Now));
        Assert.False(change.TryReview(true, Guid.NewGuid(), "bad\0reason", Now)); Assert.False(change.TryReview(true, Guid.NewGuid(), new string('a', 1001), Now));
        Assert.False(change.TryReview(true, Guid.NewGuid(), null, default)); Assert.Equal(ActivityChangeStatus.Pending, change.Status); Assert.Null(change.ReviewedBy);
        Assert.True(change.TryReview(true, Guid.NewGuid(), null, Now));
    }
    [Fact]
    public void Empty_or_unchanged_activity_proposals_are_invalid()
    {
        var activity = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => StoreActivityChange.Create(Guid.NewGuid(), activity, activity, Guid.NewGuid(), null, Now));
        Assert.Throws<ArgumentException>(() => StoreActivityChange.Create(Guid.NewGuid(), activity, Guid.Empty, Guid.NewGuid(), null, Now));
        Assert.Throws<ArgumentException>(() => StoreActivityChange.Create(Guid.Empty, activity, Guid.NewGuid(), Guid.NewGuid(), null, Now));
        Assert.Throws<ArgumentException>(() => StoreActivityChange.Create(Guid.NewGuid(), activity, Guid.NewGuid(), Guid.Empty, null, Now));
    }
    [Fact]
    public void Applying_a_reviewed_activity_requires_the_expected_current_activity()
    {
        Assert.True(StoreDetails.TryCreate("store", "+201012345678", null, null, null, null, out var details)); var activity = Guid.NewGuid(); var target = Guid.NewGuid();
        var store = Store.Create(Guid.NewGuid(), activity, details!);
        Assert.False(store.TryApplyApprovedActivity(Guid.NewGuid(), target)); Assert.Equal(activity, store.ActivityId);
        Assert.False(store.TryApplyApprovedActivity(activity, Guid.Empty)); Assert.False(store.TryApplyApprovedActivity(activity, activity));
        Assert.True(store.TryApplyApprovedActivity(activity, target)); Assert.Equal(target, store.ActivityId); Assert.False(store.TryApplyApprovedActivity(activity, Guid.NewGuid()));
    }
    [Fact]
    public void Wire_versions_reasons_and_named_list_filters_are_validated()
    {
        var version = Convert.ToBase64String(new byte[8]); var write = new ActivityChangeWrite(Guid.NewGuid(), version);
        Assert.True(ActivityChangeInput.Write(write)); Assert.False(ActivityChangeInput.Write(write with { StoreRowVersion = "invalid" }));
        Assert.False(ActivityChangeInput.Write(write with { ActivityId = Guid.Empty })); Assert.False(ActivityChangeInput.Write(write with { Reason = new string('a', 1001) }));
        Assert.True(ActivityChangeInput.Review(new(version, version), true)); Assert.False(ActivityChangeInput.Review(new(version, version), false));
        Assert.True(ActivityChangeInput.Review(new(version, version, "reason"), false)); Assert.False(ActivityChangeInput.Review(new(version, "invalid", "reason"), false));
        Assert.True(ActivityChangeInput.Status("Pending")); Assert.True(ActivityChangeInput.Status("approved")); Assert.True(ActivityChangeInput.Status(null)); Assert.False(ActivityChangeInput.Status("1"));
    }
}
