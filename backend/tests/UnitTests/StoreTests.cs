using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Domain.Modules.Merchants;

namespace HawalekNow.UnitTests;

public sealed class StoreTests
{
    [Theory]
    [InlineData("01012345678", null, "+201012345678", null)]
    [InlineData(null, "+201012345678", null, "+201012345678")]
    [InlineData("+44 (20) 1234-5678", "+201112345678", "+442012345678", "+201112345678")]
    public void One_contact_is_enough_and_each_channel_is_normalized_independently(string? phone, string? whatsApp, string? expectedPhone, string? expectedWhatsApp)
    {
        Assert.True(StoreDetails.TryCreate("  متجر نور  ", phone, whatsApp, null, null, null, out var details));
        Assert.Equal("متجر نور", details!.Name); Assert.Equal(expectedPhone, details.Phone); Assert.Equal(expectedWhatsApp, details.WhatsApp);
        var owner = Guid.NewGuid(); var activity = Guid.NewGuid(); var store = Store.Create(owner, activity, details);
        Assert.Equal(owner, store.MerchantAccountId); Assert.Equal(activity, store.ActivityId); Assert.Null(store.LogoKey);
        Assert.True(StoreDetails.TryCreate("New name", "+201212345678", null, "https://www.facebook.com/shop", null, null, out var edit));
        store.Update(edit!); Assert.Equal(activity, store.ActivityId); Assert.Equal(owner, store.MerchantAccountId); Assert.Equal("New name", store.Name);
    }
    [Theory]
    [InlineData(null, null)]
    [InlineData("", " ")]
    [InlineData("invalid", "+201012345678")]
    [InlineData("+001012345678", null)]
    [InlineData("+2010\n12345678", null)]
    [InlineData("+201012345678901234", null)]
    public void Missing_or_invalid_contacts_are_rejected(string? phone, string? whatsApp) =>
        Assert.False(StoreDetails.TryCreate("Store", phone, whatsApp, null, null, null, out _));

    [Theory]
    [InlineData("http://facebook.com/shop", null, null)]
    [InlineData("https://facebook.com.evil.invalid/shop", null, null)]
    [InlineData("https://someone@facebook.com/shop", null, null)]
    [InlineData("https://facebook.com:444/shop", null, null)]
    [InlineData(null, "https://facebook.com/shop", null)]
    [InlineData(null, null, "javascript:alert(1)")]
    public void Optional_social_links_require_the_correct_https_service(string? facebook, string? instagram, string? tikTok) =>
        Assert.False(StoreDetails.TryCreate("Store", "+201012345678", null, facebook, instagram, tikTok, out _));

    [Fact]
    public void Name_and_url_bounds_and_application_validation_are_enforced()
    {
        foreach (var name in new[] { "", " ", "line\nbreak", new string('x', 201) })
            Assert.False(StoreDetails.TryCreate(name, "+201012345678", null, null, null, null, out _));
        Assert.False(StoreDetails.TryCreate("Store", "+201012345678", null, "https://facebook.com/" + new string('x', 500), null, null, out _));
        Assert.True(StoreInput.Details(new("One name", null, "+201012345678", "https://facebook.com/shop", "https://instagram.com/shop", "https://vm.tiktok.com/code")));
        Assert.False(new CreateStoreValidator().Validate(new CreateStoreCommand(Guid.NewGuid(), new(Guid.Empty, new("Store", "+201012345678")))).IsValid);
        Assert.False(new UpdateStoreValidator().Validate(new UpdateStoreCommand(Guid.NewGuid(), Guid.NewGuid(), new("stale-invalid", new("Store", "+201012345678")))).IsValid);
        Assert.False(new ChangeStoreLogoValidator().Validate(new ChangeStoreLogoCommand(Guid.Empty, Guid.NewGuid(), Convert.ToBase64String(new byte[8]), null)).IsValid);
    }
}
