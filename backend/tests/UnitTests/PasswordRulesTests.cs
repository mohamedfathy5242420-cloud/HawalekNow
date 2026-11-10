using HawalekNow.Domain.Modules.Identity;

namespace HawalekNow.UnitTests;

public sealed class PasswordRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("               ")]
    [InlineData("short-password")]
    [InlineData("passwordpassword")]
    [InlineData("PASSWORD123456789")]
    public void Blank_short_and_common_passwords_are_rejected(string? value) => Assert.False(PasswordRules.IsAcceptable(value));

    [Fact]
    public void Unicode_length_counts_runes_and_supports_long_passphrases_without_composition_rules()
    {
        Assert.False(PasswordRules.IsAcceptable(string.Concat(Enumerable.Repeat("😀", 14))));
        Assert.True(PasswordRules.IsAcceptable(string.Concat(Enumerable.Repeat("😀", 15))));
        Assert.True(PasswordRules.IsAcceptable("a long lower case passphrase"));
        Assert.True(PasswordRules.IsAcceptable(string.Concat(Enumerable.Repeat("ab", 64))));
        Assert.False(PasswordRules.IsAcceptable(string.Concat(Enumerable.Repeat("ab", 65))));
    }
}
