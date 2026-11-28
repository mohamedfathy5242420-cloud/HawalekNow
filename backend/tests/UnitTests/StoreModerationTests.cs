using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Infrastructure.Modules.Merchants;

namespace HawalekNow.UnitTests;

public sealed class StoreModerationTests
{
    private static Store Store()
    {
        Assert.True(StoreDetails.TryCreate("store", "+201012345678", null, null, null, null, out var details));
        return HawalekNow.Domain.Modules.Merchants.Store.Create(Guid.NewGuid(), Guid.NewGuid(), details!);
    }
    [Fact]
    public void A_new_store_has_no_moderation_restriction_but_account_approval_stays_a_separate_gate()
    {
        var store = Store(); Assert.False(store.IsHidden); Assert.False(store.IsSuspended); Assert.True(store.CanOwnerEdit); Assert.True(store.CanAppearPublicly);
        Assert.Null(store.VisibilityReason); Assert.Null(store.SuspensionReason);
    }
    [Fact]
    public void Hiding_prevents_public_appearance_and_keeps_owner_preparation_available()
    {
        var store = Store(); Assert.True(store.TryModerate(StoreModerationAction.Hide, " visibility review "));
        Assert.True(store.IsHidden); Assert.False(store.CanAppearPublicly); Assert.True(store.CanOwnerEdit); Assert.Equal("visibility review", store.VisibilityReason);
        Assert.False(store.TryModerate(StoreModerationAction.Hide, "duplicate")); Assert.Equal("visibility review", store.VisibilityReason);
        Assert.True(store.TryModerate(StoreModerationAction.Publish, "review resolved")); Assert.True(store.CanAppearPublicly); Assert.True(store.CanOwnerEdit);
    }
    [Fact]
    public void Suspension_blocks_owner_mutation_and_public_appearance_and_private_DTO_explains_it()
    {
        var store = Store(); Assert.True(store.TryModerate(StoreModerationAction.Suspend, "commercial review"));
        Assert.False(store.CanOwnerEdit); Assert.False(store.CanAppearPublicly); var dto = StoreService.ToDto(store);
        Assert.True(dto.IsSuspended); Assert.Equal("commercial review", dto.SuspensionReason); Assert.False(dto.CanOwnerEdit);
        Assert.True(store.TryModerate(StoreModerationAction.Resume, "resolved")); Assert.True(store.CanOwnerEdit); Assert.True(store.CanAppearPublicly);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Clearing_one_restriction_never_clears_the_other(bool resumeFirst)
    {
        var store = Store(); Assert.True(store.TryModerate(StoreModerationAction.Hide, "hidden")); Assert.True(store.TryModerate(StoreModerationAction.Suspend, "suspended"));
        Assert.True(store.TryModerate(resumeFirst ? StoreModerationAction.Resume : StoreModerationAction.Publish, "first resolved"));
        Assert.False(store.CanAppearPublicly); Assert.Equal(!resumeFirst, store.IsSuspended); Assert.Equal(resumeFirst, store.IsHidden);
        Assert.True(store.TryModerate(resumeFirst ? StoreModerationAction.Publish : StoreModerationAction.Resume, "second resolved")); Assert.True(store.CanAppearPublicly);
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("bad\0reason")]
    public void A_required_valid_reason_precedes_any_state_change(string? reason)
    {
        var store = Store(); Assert.False(store.TryModerate(StoreModerationAction.Hide, reason)); Assert.False(store.TryModerate(StoreModerationAction.Suspend, reason));
        Assert.True(store.CanOwnerEdit); Assert.True(store.CanAppearPublicly); Assert.Null(store.VisibilityReason); Assert.Null(store.SuspensionReason);
    }
    [Fact]
    public void Unknown_or_noop_transitions_and_overlong_reasons_are_rejected()
    {
        var store = Store(); Assert.False(store.TryModerate((StoreModerationAction)99, "reason")); Assert.False(store.TryModerate(StoreModerationAction.Publish, "reason"));
        Assert.False(store.TryModerate(StoreModerationAction.Resume, "reason")); Assert.False(store.TryModerate(StoreModerationAction.Hide, new string('a', 1001)));
        Assert.True(store.TryModerate(StoreModerationAction.Hide, new string('a', 1000)));
    }
    [Fact]
    public void Admin_contracts_require_actor_versions_reasons_and_valid_details()
    {
        var actor = Guid.NewGuid(); var id = Guid.NewGuid(); var version = Convert.ToBase64String(new byte[8]);
        var moderation = new ModerateStoreCommand(actor, id, StoreAdministrationAction.Hide, new(version, "reason"));
        Assert.True(new ModerateStoreValidator().Validate(moderation).IsValid); Assert.False(new ModerateStoreValidator().Validate(moderation with { Action = (StoreAdministrationAction)99 }).IsValid);
        Assert.False(new ModerateStoreValidator().Validate(moderation with { ActorId = Guid.Empty }).IsValid); Assert.False(new ModerateStoreValidator().Validate(moderation with { Value = new("invalid", "reason") }).IsValid);
        var edit = new UpdateAdminStoreCommand(actor, id, new(version, new("name", "+201012345678"), "reason"));
        Assert.True(new UpdateAdminStoreValidator().Validate(edit).IsValid); Assert.False(new UpdateAdminStoreValidator().Validate(edit with { Value = edit.Value with { Reason = " " } }).IsValid);
        Assert.False(new UpdateAdminStoreValidator().Validate(edit with { Value = edit.Value with { Details = new("name") } }).IsValid);
    }
}
