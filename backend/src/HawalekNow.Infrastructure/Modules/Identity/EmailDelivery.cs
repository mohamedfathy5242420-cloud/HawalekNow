using System.Net.Mail;
using HawalekNow.Application.Shared.Messaging;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Infrastructure.Shared.Messaging;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed record IdentityEmail(string Recipient, string Subject, string Text, Guid MessageId);
public interface IIdentityEmailTransport { Task SendAsync(IdentityEmail email, CancellationToken cancellationToken); }

public sealed class MailpitEmailTransport(IConfiguration configuration, IHostEnvironment environment) : IIdentityEmailTransport
{
    public async Task SendAsync(IdentityEmail email, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment()) throw new InvalidOperationException("Mailpit transport is restricted to Development.");
        var host = configuration["Email:SmtpHost"] ?? "127.0.0.1";
        if (host is not "127.0.0.1" and not "localhost" and not "::1") throw new InvalidOperationException("Development SMTP must use loopback.");
        var port = configuration.GetValue<int>("Email:SmtpPort");
        if (port is < 1 or > 65535) throw new InvalidOperationException("Configure the local Mailpit SMTP port.");
        using var message = new MailMessage("no-reply@hawaleknow.invalid", email.Recipient, email.Subject, email.Text);
        message.Headers.Add("X-HawalekNow-Message-ID", email.MessageId.ToString("N"));
        using var smtp = new SmtpClient(host, port) { EnableSsl = false, DeliveryFormat = SmtpDeliveryFormat.International };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try { await smtp.SendMailAsync(message, timeout.Token); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { throw new InvalidOperationException("Email delivery failed."); }
    }
}

public sealed record EmailOtpPayload(Guid UserId, OtpPurpose Purpose, Guid Generation, string Email, string Code);
public sealed class EmailOtpHandler(HawalekDbContext context, PayloadProtection protection, IIdentityEmailTransport transport, TimeProvider clock) : IOutboxHandler
{
    public const string MessageKind = "identity.email-otp.v1";
    public string Kind => MessageKind;
    public async Task<OutboxHandlingResult> HandleAsync(Guid messageId, string protectedPayload, CancellationToken cancellationToken)
    {
        var payload = protection.Unprotect<EmailOtpPayload>(Kind, protectedPayload);
        var now = clock.GetUtcNow();
        var valid = await context.Set<OtpChallenge>().AsNoTracking().AnyAsync(value => value.UserId == payload.UserId && value.Purpose == payload.Purpose &&
            value.Generation == payload.Generation && value.ConsumedAt == null && value.ExpiresAt > now, cancellationToken);
        if (!valid || !await context.Set<ApplicationUser>().AnyAsync(value => value.Id == payload.UserId, cancellationToken)) return OutboxHandlingResult.Suppressed;
        await transport.SendAsync(new(payload.Email, "HawalekNow verification code", $"Purpose: {payload.Purpose}\nCode: {payload.Code}\nExpires in ten minutes.\n", messageId), cancellationToken);
        return OutboxHandlingResult.Completed;
    }
}
