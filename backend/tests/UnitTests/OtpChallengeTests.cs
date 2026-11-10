using HawalekNow.Domain.Modules.Identity;

namespace HawalekNow.UnitTests;

public sealed class OtpChallengeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 0, 0, 0, TimeSpan.Zero);
    private static OtpChallenge Issued()
    {
        var challenge = OtpChallenge.Create(Guid.CreateVersion7(), OtpPurpose.EmailConfirmation);
        Assert.True(challenge.Issue(Guid.CreateVersion7(), new string('A', 64), Now)); return challenge;
    }
    [Fact]
    public void Expiry_boundary_and_single_use_are_enforced()
    {
        Assert.False(Issued().Consume(true, Now.AddMinutes(10)));
        var challenge = Issued(); Assert.True(challenge.Consume(true, Now.AddMinutes(9))); Assert.False(challenge.Consume(true, Now.AddMinutes(9)));
    }
    [Fact]
    public void Resend_cannot_reset_attempts_or_bypass_cooldown_and_window_limit()
    {
        var challenge = Issued();
        for (var attempt = 0; attempt < 4; attempt++) Assert.False(challenge.Consume(false, Now));
        Assert.False(challenge.Issue(Guid.CreateVersion7(), new string('B', 64), Now.AddSeconds(59)));
        Assert.True(challenge.Issue(Guid.CreateVersion7(), new string('B', 64), Now.AddMinutes(1)));
        Assert.Equal(4, challenge.FailedAttempts);
        Assert.False(challenge.Consume(false, Now.AddMinutes(1)));
        Assert.False(challenge.Consume(true, Now.AddMinutes(1)));
        Assert.False(challenge.Issue(Guid.CreateVersion7(), new string('C', 64), Now.AddMinutes(2)));
        Assert.True(challenge.Issue(Guid.CreateVersion7(), new string('D', 64), Now.AddMinutes(31)));
        Assert.True(challenge.Consume(true, Now.AddMinutes(31)));
    }
    [Fact]
    public void Only_three_issues_are_allowed_within_fifteen_minutes()
    {
        var challenge = Issued();
        Assert.True(challenge.Issue(Guid.CreateVersion7(), new string('B', 64), Now.AddMinutes(1)));
        Assert.True(challenge.Issue(Guid.CreateVersion7(), new string('C', 64), Now.AddMinutes(2)));
        Assert.False(challenge.Issue(Guid.CreateVersion7(), new string('D', 64), Now.AddMinutes(3)));
        Assert.True(challenge.Issue(Guid.CreateVersion7(), new string('E', 64), Now.AddMinutes(15)));
    }
}
