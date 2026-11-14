using HawalekNow.Domain.Modules.Identity;

namespace HawalekNow.UnitTests;

public sealed class AccountSessionTests
{
    [Fact]
    public void Idle_expiry_and_revocation_are_exact_and_revocation_is_idempotent()
    {
        var now = DateTimeOffset.UtcNow; var session = AccountSession.Create(Guid.CreateVersion7(), now);
        Assert.True(session.IsActive(now.AddDays(7).AddTicks(-1)));
        Assert.False(session.IsActive(now.AddDays(7))); Assert.False(session.Rotate(now.AddDays(7)));
        session.Revoke(now.AddMinutes(1)); session.Revoke(now.AddMinutes(2));
        Assert.Equal(now.AddMinutes(1), session.RevokedAt); Assert.False(session.Rotate(now.AddMinutes(3)));
    }
    [Fact]
    public void Rotation_extends_idle_expiry_but_never_the_absolute_thirty_days()
    {
        var now = DateTimeOffset.UtcNow; var session = AccountSession.Create(Guid.CreateVersion7(), now);
        foreach (var day in new[] { 6, 12, 18, 24, 29 }) Assert.True(session.Rotate(now.AddDays(day)));
        Assert.Equal(now.AddDays(30), session.RefreshExpiresAt); Assert.Equal(now.AddDays(30), session.AbsoluteExpiresAt);
        Assert.False(session.IsActive(now.AddDays(30))); Assert.False(session.Rotate(now.AddDays(30)));
    }
}
