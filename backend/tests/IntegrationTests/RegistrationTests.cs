using System.Net;
using System.Net.Http.Json;
using HawalekNow.Application.Modules.Merchants.Contracts;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Infrastructure.Modules.Identity;
using HawalekNow.Infrastructure.Modules.Merchants;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HawalekNow.IntegrationTests;

public sealed class RegistrationTests(SqlDatabase database) : IClassFixture<SqlDatabase>
{
    private static (string Name, string Email, string Password) Details()
    {
        var name = "reg" + Guid.NewGuid().ToString("N");
        return (name, name + "@example.invalid", "Test-only-" + Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public async Task Separate_routes_keep_customer_and_merchant_independent_and_ignore_privilege_and_owner_input()
    {
        var details = Details();
        var suppliedId = Guid.CreateVersion7();
        var clock = new ManualTimeProvider(new(2026, 11, 10, 0, 0, 0, TimeSpan.Zero));
        await using var factory = new IdentityApiFactory(database, clock);
        using var client = factory.CreateHttpsClient();
        var body = new { username = details.Name, email = details.Email, password = details.Password, acceptTerms = true, accountType = "Admin", roles = new[] { "Admin" }, userId = suppliedId };
        using var customer = await client.PostBrowserJsonAsync("/api/v1/identity/customers/register", body);
        using var merchant = await client.PostBrowserJsonAsync("/api/v1/identity/merchants/register", body);
        foreach (var response in new[] { customer, merchant })
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal("", await response.Content.ReadAsStringAsync());
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }
        await using var context = database.CreateContext();
        var users = await context.Set<ApplicationUser>().Where(user => user.UserName == details.Name).OrderBy(user => user.AccountType).ToArrayAsync();
        Assert.Equal(2, users.Length);
        Assert.Equal(AccountType.Customer, users[0].AccountType);
        Assert.Equal(AccountType.Merchant, users[1].AccountType);
        Assert.NotEqual(users[0].Id, users[1].Id);
        Assert.All(users, user =>
        {
            Assert.NotEqual(suppliedId, user.Id);
            Assert.False(user.EmailConfirmed);
            Assert.NotEqual(details.Password, user.PasswordHash);
            Assert.Equal(clock.GetUtcNow(), user.TermsAcceptedAt);
        });
        var profile = await context.Set<MerchantAccount>().SingleAsync(account => account.UserId == users[1].Id);
        Assert.Equal(MerchantApprovalStatus.Pending, profile.Status);
        Assert.False(await context.Set<MerchantAccount>().AnyAsync(account => account.UserId == users[0].Id));
    }

    [Fact]
    public async Task Concurrent_and_case_variant_duplicates_receive_identical_acknowledgements_and_create_one_account()
    {
        var details = Details();
        await using var factory = new IdentityApiFactory(database);
        using var client = factory.CreateHttpsClient();
        var body = new { username = details.Name, email = details.Email, password = details.Password, acceptTerms = true };
        async Task<HttpResponseMessage> FromPeerAsync(int index)
        {
            using var peer = new HttpClient(factory.Server.CreateHandler(context => context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2." + (index + 1))))
            { BaseAddress = new("https://localhost") };
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/merchants/register") { Content = JsonContent.Create(body) };
            await BrowserRequests.AddCsrfAsync(peer, request, rawCookie: "");
            return await peer.SendAsync(request);
        }
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(FromPeerAsync));
        foreach (var response in responses) { using (response) { Assert.Equal(HttpStatusCode.NoContent, response.StatusCode); Assert.Equal("", await response.Content.ReadAsStringAsync()); } }
        using var duplicateName = await client.PostBrowserJsonAsync("/api/v1/identity/merchants/register", new { username = details.Name.ToUpperInvariant(), email = "other-" + details.Email, password = details.Password, acceptTerms = true });
        using var duplicateEmail = await client.PostBrowserJsonAsync("/api/v1/identity/merchants/register", new { username = "other" + details.Name, email = details.Email.ToUpperInvariant(), password = details.Password, acceptTerms = true });
        Assert.Equal(HttpStatusCode.NoContent, duplicateName.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, duplicateEmail.StatusCode);
        await using var context = database.CreateContext();
        var normalized = details.Name.ToUpperInvariant();
        var user = Assert.Single(await context.Set<ApplicationUser>().Where(value => value.NormalizedUserName == normalized).ToArrayAsync());
        Assert.Equal(1, await context.Set<MerchantAccount>().CountAsync(value => value.UserId == user.Id));
        Assert.False(await context.Set<ApplicationUser>().AnyAsync(value => value.UserName == "other" + details.Name));
    }

    [Theory]
    [InlineData("terms")]
    [InlineData("password")]
    [InlineData("email")]
    [InlineData("username")]
    public async Task Invalid_registration_uses_existing_safe_validation_contract_without_echoing_values(string invalid)
    {
        var details = Details();
        var name = invalid == "username" ? "invalid@" + details.Name : details.Name;
        var password = invalid == "password" ? "secret-short" : details.Password;
        await using var factory = new IdentityApiFactory(database);
        using var client = factory.CreateHttpsClient();
        using var response = await client.PostBrowserJsonAsync("/api/v1/identity/customers/register", new
        {
            username = name,
            email = invalid == "email" ? "invalid-email" : details.Email,
            password,
            acceptTerms = invalid != "terms"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(!text.Contains(password, StringComparison.Ordinal) && !text.Contains(details.Email, StringComparison.Ordinal), "Submitted values were reflected.");
        await using var context = database.CreateContext();
        Assert.False(await context.Set<ApplicationUser>().AnyAsync(user => user.UserName == name));
    }

    [Fact]
    public async Task A_partial_merchant_failure_rolls_back_the_Identity_store_and_merchant_profile_through_actual_HTTP()
    {
        var details = Details();
        var observation = new FailureObservation();
        await using var factory = new IdentityApiFactory(database, configure: services =>
        {
            services.AddSingleton(observation);
            services.Replace(ServiceDescriptor.Scoped<IMerchantAccounts, FailingMerchants>());
        });
        using var client = factory.CreateHttpsClient();
        using var response = await client.PostBrowserJsonAsync("/api/v1/identity/merchants/register", new { username = details.Name, email = details.Email, password = details.Password, acceptTerms = true });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("test-only-failure-secret", await response.Content.ReadAsStringAsync());
        await using var context = database.CreateContext();
        Assert.False(await context.Set<ApplicationUser>().AnyAsync(user => user.UserName == details.Name));
        Assert.NotEqual(Guid.Empty, observation.OwnerId);
        Assert.False(await context.Set<MerchantAccount>().AnyAsync(account => account.UserId == observation.OwnerId));
    }

    [Fact]
    public async Task Permanent_identifier_reservations_block_only_the_same_account_type_without_exposing_original_values()
    {
        var details = Details();
        await using var factory = new IdentityApiFactory(database);
        using var client = factory.CreateHttpsClient();
        using (var scope = factory.Services.CreateScope())
        {
            var digests = scope.ServiceProvider.GetRequiredService<IdentifierDigests>();
            var normalizer = scope.ServiceProvider.GetRequiredService<ILookupNormalizer>();
            await using var context = database.CreateContext();
            context.Add(ReservedIdentifier.Create(AccountType.Customer, IdentifierKind.Email,
                digests.Hash(AccountType.Customer, IdentifierKind.Email, normalizer.NormalizeEmail(details.Email)!)));
            await context.SaveChangesAsync();
        }
        var body = new { username = details.Name, email = details.Email, password = details.Password, acceptTerms = true };
        using var customer = await client.PostBrowserJsonAsync("/api/v1/identity/customers/register", body);
        using var merchant = await client.PostBrowserJsonAsync("/api/v1/identity/merchants/register", body);
        Assert.Equal(HttpStatusCode.NoContent, customer.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, merchant.StatusCode);
        await using var verify = database.CreateContext();
        var users = await verify.Set<ApplicationUser>().Where(user => user.UserName == details.Name).ToArrayAsync();
        Assert.Equal(AccountType.Merchant, Assert.Single(users).AccountType);
        var reservation = await verify.Set<ReservedIdentifier>().SingleAsync(value => value.AccountType == AccountType.Customer);
        Assert.Equal(64, reservation.Digest.Length);
        Assert.DoesNotContain(details.Email, reservation.Digest);
    }

    private sealed class FailureObservation { public Guid OwnerId { get; set; } }

    private sealed class FailingMerchants(HawalekDbContext context, FailureObservation observation) : IMerchantAccounts
    {
        public async Task CreatePendingAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            observation.OwnerId = userId;
            await new MerchantAccounts(context).CreatePendingAsync(userId, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("test-only-failure-secret");
        }
        public Task<MerchantAccountInfo?> FindByOwnerAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<MerchantAccountInfo?>(null);
    }
}
