using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Domain.Shared.Auditing;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.IntegrationTests;

public sealed class MerchantReviewHttpTests(SqlDatabase database) : IClassFixture<SqlDatabase>, IAsyncLifetime
{
    private readonly IdentityApiFactory factory = new(database);
    private HttpClient adminClient = null!;
    private HttpClient merchantClient = null!;
    public Task InitializeAsync() { adminClient = factory.CreateHttpsClient(); merchantClient = factory.CreateHttpsClient(); return Task.CompletedTask; }
    public async Task DisposeAsync() { adminClient.Dispose(); merchantClient.Dispose(); await factory.DisposeAsync(); }
    private static string AdminPath(Guid id, string decision) => $"/api/v1/admin/merchants/{id}/{decision}";
    private async Task<MerchantReviewDto> DecideAsync(Guid id, string decision, string version, string? reason = null)
    {
        using var response = await adminClient.PostBrowserJsonAsync(AdminPath(id, decision), new MerchantReviewWrite(version, reason));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MerchantReviewDto>())!;
    }
    private static async Task ProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "code", "correlationId", "status", "title", "type" }, body.RootElement.EnumerateObject().Select(item => item.Name).Order().ToArray());
    }

    [Fact]
    public async Task Changes_resubmission_and_approval_use_real_sessions_and_audited_server_identity()
    {
        var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin);
        var merchant = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant);
        var other = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant);
        await MerchantTestAccounts.LoginAsync(adminClient, admin); await MerchantTestAccounts.LoginAsync(merchantClient, merchant);
        var pending = (await merchantClient.GetFromJsonAsync<MerchantReviewDto>("/api/v1/merchants/review"))!;
        Assert.Equal("Pending", pending.Status); Assert.True(pending.EmailConfirmed);
        using (var publication = await merchantClient.GetAsync("/api/v1/merchants/account/publishing-access")) await ProblemAsync(publication, HttpStatusCode.Forbidden);
        var listed = (await adminClient.GetFromJsonAsync<MerchantPage<MerchantReviewDto>>("/api/v1/admin/merchants?status=Pending&pageSize=100"))!;
        Assert.Contains(listed.Items, item => item.Id == merchant.ProfileId && item.Email == merchant.Email);
        using (var invalid = await adminClient.PostBrowserJsonAsync(AdminPath(merchant.ProfileId, "request-changes"), new MerchantReviewWrite(pending.RowVersion, " ")))
            await ProblemAsync(invalid, HttpStatusCode.BadRequest);
        var changes = await DecideAsync(merchant.ProfileId, "request-changes", pending.RowVersion, "  Correct the contact number.  ");
        Assert.Equal("ChangesRequested", changes.Status); Assert.Equal("Correct the contact number.", changes.ReviewReason);
        using (var setup = await merchantClient.GetAsync("/api/v1/merchants/account")) Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        using var otherClient = factory.CreateHttpsClient(); await MerchantTestAccounts.LoginAsync(otherClient, other);
        using (var spoof = await otherClient.PostBrowserJsonAsync("/api/v1/merchants/review/resubmit", new { rowVersion = changes.RowVersion, actorId = merchant.Id, merchantAccountId = merchant.ProfileId }))
            await ProblemAsync(spoof, HttpStatusCode.Conflict);
        var own = (await merchantClient.GetFromJsonAsync<MerchantReviewDto>($"/api/v1/merchants/review?userId={other.Id}&merchantAccountId={other.ProfileId}"))!;
        Assert.Equal(merchant.ProfileId, own.Id);
        MerchantReviewDto resubmitted;
        using (var response = await merchantClient.PostBrowserJsonAsync("/api/v1/merchants/review/resubmit", new MerchantResubmitWrite(own.RowVersion)))
        { Assert.Equal(HttpStatusCode.OK, response.StatusCode); resubmitted = (await response.Content.ReadFromJsonAsync<MerchantReviewDto>())!; }
        Assert.Equal("Pending", resubmitted.Status);
        using (var stale = await adminClient.PostBrowserJsonAsync(AdminPath(merchant.ProfileId, "approve"), new MerchantReviewWrite(pending.RowVersion)))
            await ProblemAsync(stale, HttpStatusCode.Conflict);
        using (var approved = await adminClient.PostBrowserJsonAsync(AdminPath(merchant.ProfileId, "approve"), new { rowVersion = resubmitted.RowVersion, actorId = other.Id }))
        { Assert.Equal(HttpStatusCode.OK, approved.StatusCode); Assert.Equal("Approved", (await approved.Content.ReadFromJsonAsync<MerchantReviewDto>())!.Status); }
        using (var publication = await merchantClient.GetAsync("/api/v1/merchants/account/publishing-access")) Assert.Equal(HttpStatusCode.OK, publication.StatusCode);
        var history = (await adminClient.GetFromJsonAsync<MerchantPage<MerchantAuditDto>>($"/api/v1/admin/merchants/{merchant.ProfileId}/audit"))!;
        Assert.Equal(3, history.Items.Length);
        Assert.All(history.Items, item => Assert.Equal(item.Operation == "Merchant.Resubmit" ? merchant.Id : admin.Id, item.ActorId));
        var json = JsonSerializer.Serialize(history);
        Assert.DoesNotContain(admin.Password, json); Assert.DoesNotContain(merchant.Password, json); Assert.DoesNotContain(other.Email, json);
        using (var privateAudit = await merchantClient.GetAsync($"/api/v1/admin/merchants/{merchant.ProfileId}/audit")) await ProblemAsync(privateAudit, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rejection_keeps_Identity_account_management_but_blocks_setup_and_self_resubmission()
    {
        var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin); var merchant = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant);
        await MerchantTestAccounts.LoginAsync(adminClient, admin); await MerchantTestAccounts.LoginAsync(merchantClient, merchant);
        var rejected = await DecideAsync(merchant.ProfileId, "reject", await MerchantTestAccounts.VersionAsync(database, merchant.ProfileId), "Duplicate merchant account.");
        using (var setup = await merchantClient.GetAsync("/api/v1/merchants/account")) await ProblemAsync(setup, HttpStatusCode.Forbidden);
        using (var identity = await merchantClient.GetAsync("/api/v1/identity/me")) Assert.Equal(HttpStatusCode.OK, identity.StatusCode);
        var review = (await merchantClient.GetFromJsonAsync<MerchantReviewDto>("/api/v1/merchants/review"))!;
        Assert.Equal("Rejected", review.Status); Assert.Equal("Duplicate merchant account.", review.ReviewReason);
        using var resubmit = await merchantClient.PostBrowserJsonAsync("/api/v1/merchants/review/resubmit", new MerchantResubmitWrite(rejected.RowVersion));
        await ProblemAsync(resubmit, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Suspension_and_reinstatement_apply_live_to_the_same_cookie()
    {
        var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin); var merchant = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant);
        await MerchantTestAccounts.LoginAsync(adminClient, admin); await MerchantTestAccounts.LoginAsync(merchantClient, merchant);
        var approved = await DecideAsync(merchant.ProfileId, "approve", await MerchantTestAccounts.VersionAsync(database, merchant.ProfileId));
        using (var access = await merchantClient.GetAsync("/api/v1/merchants/account/publishing-access")) Assert.Equal(HttpStatusCode.OK, access.StatusCode);
        var suspended = await DecideAsync(merchant.ProfileId, "suspend", approved.RowVersion, "Manual suspension.");
        using (var access = await merchantClient.GetAsync("/api/v1/merchants/review")) await ProblemAsync(access, HttpStatusCode.Unauthorized);
        using (var invalid = await adminClient.PostBrowserJsonAsync(AdminPath(merchant.ProfileId, "reinstate"), new MerchantReviewWrite(suspended.RowVersion)))
            await ProblemAsync(invalid, HttpStatusCode.BadRequest);
        var reinstated = await DecideAsync(merchant.ProfileId, "reinstate", suspended.RowVersion, "Issue resolved.");
        Assert.Equal("Approved", reinstated.Status);
        using var restored = await merchantClient.GetAsync("/api/v1/merchants/account/publishing-access"); Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(AccountType.Customer, HttpStatusCode.Forbidden)]
    [InlineData(AccountType.Merchant, HttpStatusCode.Forbidden)]
    public async Task Guests_and_non_admins_cannot_review_or_read_audit(AccountType? type, HttpStatusCode expected)
    {
        if (type is not null) await MerchantTestAccounts.LoginAsync(adminClient, await MerchantTestAccounts.CreateAsync(factory, type.Value));
        var id = Guid.CreateVersion7();
        using (var list = await adminClient.GetAsync("/api/v1/admin/merchants")) await ProblemAsync(list, expected);
        using (var audit = await adminClient.GetAsync($"/api/v1/admin/merchants/{id}/audit")) await ProblemAsync(audit, expected);
        foreach (var action in new[] { "approve", "request-changes", "reject", "suspend", "reinstate" })
        {
            using var response = await adminClient.PostBrowserJsonAsync(AdminPath(id, action), new MerchantReviewWrite("AAAAAAAAAAA=", "Manual decision."));
            await ProblemAsync(response, expected);
        }
    }

    [Fact]
    public async Task Review_mutations_require_csrf_https_valid_versions_and_confirmed_target_email()
    {
        var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin); var unconfirmed = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant, false);
        await MerchantTestAccounts.LoginAsync(adminClient, admin);
        var path = AdminPath(unconfirmed.ProfileId, "approve"); var version = await MerchantTestAccounts.VersionAsync(database, unconfirmed.ProfileId);
        using (var noCsrf = await adminClient.PostAsJsonAsync(path, new MerchantReviewWrite(version))) await ProblemAsync(noCsrf, HttpStatusCode.BadRequest);
        using (var invalid = await adminClient.PostBrowserJsonAsync(path, new MerchantReviewWrite("invalid-version"))) await ProblemAsync(invalid, HttpStatusCode.BadRequest);
        using (var invalid = await adminClient.PostBrowserJsonAsync(path, new MerchantReviewWrite(version, new string('x', 1001)))) await ProblemAsync(invalid, HttpStatusCode.BadRequest);
        using (var premature = await adminClient.PostBrowserJsonAsync(path, new MerchantReviewWrite(version))) await ProblemAsync(premature, HttpStatusCode.Conflict);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost" + path) { Content = JsonContent.Create(new MerchantReviewWrite(version)) })
        {
            await BrowserRequests.AddCsrfAsync(adminClient, request); using var response = await adminClient.SendAsync(request);
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
        }
        await using var read = database.CreateContext();
        Assert.Equal(MerchantApprovalStatus.Pending, (await read.Set<MerchantAccount>().SingleAsync(item => item.Id == unconfirmed.ProfileId)).Status);
        Assert.False(await read.Set<AuditEntry>().AnyAsync(item => item.TargetId == unconfirmed.ProfileId));
    }

    [Fact]
    public async Task The_real_mutation_quota_is_shared_across_merchant_decision_routes()
    {
        var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin); await MerchantTestAccounts.LoginAsync(adminClient, admin);
        for (var i = 0; i < 60; i++)
        {
            using var response = await adminClient.PostAsJsonAsync(AdminPath(Guid.CreateVersion7(), "approve"), new MerchantReviewWrite("AAAAAAAAAAA="));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var limited = await adminClient.PostAsJsonAsync(AdminPath(Guid.CreateVersion7(), "reject"), new MerchantReviewWrite("AAAAAAAAAAA=", "Manual rejection."));
        await ProblemAsync(limited, HttpStatusCode.TooManyRequests); Assert.NotNull(limited.Headers.RetryAfter);
    }

    [Fact]
    public async Task OpenApi_exposes_decisions_versions_csrf_and_safe_error_responses()
    {
        using var response = await adminClient.GetAsync("/openapi/v1.json"); using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        foreach (var suffix in new[] { "approve", "request-changes", "reject", "suspend", "reinstate" })
        {
            var operation = paths.GetProperty("/api/v1/admin/merchants/{id}/" + suffix).GetProperty("post");
            foreach (var code in new[] { "200", "400", "401", "403", "404", "409", "429" }) Assert.True(operation.GetProperty("responses").TryGetProperty(code, out _));
            Assert.Contains(operation.GetProperty("parameters").EnumerateArray(), item => item.GetProperty("name").GetString() == "X-CSRF-TOKEN" && item.GetProperty("required").GetBoolean());
        }
        Assert.True(paths.TryGetProperty("/api/v1/merchants/review/resubmit", out _));
    }
}
