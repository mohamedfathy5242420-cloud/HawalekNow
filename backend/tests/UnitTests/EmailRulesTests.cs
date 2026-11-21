using HawalekNow.Domain.Modules.Identity;

namespace HawalekNow.UnitTests;

public sealed class EmailRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("Name <test@example.invalid>")]
    [InlineData("test@example.invalid\r\nBcc:other@example.invalid")]
    [InlineData(" test@example.invalid ")]
    public void Display_names_control_characters_and_invalid_addresses_are_not_accepted(string? value) => Assert.False(EmailRules.IsValid(value));

    [Fact]
    public void A_plain_address_is_valid_without_external_DNS_or_SMTP_lookup() => Assert.True(EmailRules.IsValid("test@example.invalid"));
}
