using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Infrastructure.Shared.Messaging;
using HawalekNow.Infrastructure.Shared.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.IntegrationTests;

// One owned SQL database and random mail recipient: the real pump cannot claim another fixture's work.
public sealed class MerchantPhaseJourneyTests : IAsyncLifetime
{
    private readonly SqlDatabase database = new();
    private readonly string root = StoreTestData.Root();
    private IdentityApiFactory factory = null!;
    public async Task InitializeAsync() { await database.InitializeAsync(); factory = new(database, settings: new Dictionary<string, string?> { ["FileStorage:Root"] = root }); }
    public async Task DisposeAsync() { await factory.DisposeAsync(); await database.DisposeAsync(); }
    [Fact]
    public async Task Real_registration_SMTP_confirmation_private_preparation_approval_and_moderation_form_one_HTTP_journey()
    {
        using var owner = factory.CreateHttpsClient(); using var admin = factory.CreateHttpsClient(); using var guest = factory.CreateHttpsClient();
        var name = "journey" + Guid.NewGuid().ToString("N"); var email = name + "@example.invalid"; var password = "Test-only-" + Guid.NewGuid().ToString("N");
        using (var registered = await owner.PostBrowserJsonAsync("/api/v1/identity/merchants/register", new { username = name, email, password, acceptTerms = true })) Assert.Equal(HttpStatusCode.NoContent, registered.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<OutboxPump>().RunAsync(default);
        var delivered = await MailpitProbe.MessagesAsync(email); Assert.True(delivered.Length == 1, "Expected this journey's one actual SMTP delivery.");
        var code = Regex.Match(delivered[0], @"(?m)^Code: ([0-9]{6})\r?$", RegexOptions.CultureInvariant); Assert.True(code.Success, "Delivered email must contain a six-digit verification code.");
        using (var verified = await owner.PostBrowserJsonAsync("/api/v1/identity/email/verify", new { accountType = "Merchant", email, code = code.Groups[1].Value })) Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        using (var loggedIn = await owner.PostBrowserJsonAsync("/api/v1/identity/login", new { accountType = "Merchant", identifier = email, password })) Assert.Equal(HttpStatusCode.NoContent, loggedIn.StatusCode);
        MerchantReviewDto review;
        using (var ownReview = await owner.GetAsync("/api/v1/merchants/review")) { Assert.Equal(HttpStatusCode.OK, ownReview.StatusCode); review = (await ownReview.Content.ReadFromJsonAsync<MerchantReviewDto>())!; Assert.True(review.EmailConfirmed); Assert.Equal("Pending", review.Status); }
        StoreDto store;
        using (var created = await owner.PostBrowserJsonAsync("/api/v1/merchants/store", new StoreCreateWrite(await StoreTestData.ActivityAsync(database), StoreTestData.Details())))
        { Assert.Equal(HttpStatusCode.OK, created.StatusCode); store = (await created.Content.ReadFromJsonAsync<StoreDto>())!; }
        BranchDto branch;
        using (var created = await owner.PostBrowserJsonAsync($"/api/v1/merchants/stores/{store.Id}/branches", BranchTestData.Write(await BranchTestData.PathAsync(database), "PickupPoint")))
        { Assert.Equal(HttpStatusCode.OK, created.StatusCode); branch = (await created.Content.ReadFromJsonAsync<BranchDto>())!; Assert.Equal(7, branch.Hours.Length); }
        using (var form = new MultipartFormDataContent())
        {
            form.Add(new StringContent(store.RowVersion), "rowVersion"); form.Add(new ByteArrayContent(StorageTestImages.Png()), "file", "selected.png");
            using var upload = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/merchants/stores/{store.Id}/logo") { Content = form }; await BrowserRequests.AddCsrfAsync(owner, upload);
            using var response = await owner.SendAsync(upload); Assert.Equal(HttpStatusCode.OK, response.StatusCode); store = (await response.Content.ReadFromJsonAsync<StoreDto>())!;
        }
        using (var stillPrivate = await guest.GetAsync($"/api/v1/stores/{store.Id}")) Assert.Equal(HttpStatusCode.NotFound, stillPrivate.StatusCode);
        var administrator = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin); await MerchantTestAccounts.LoginAsync(admin, administrator);
        using (var approved = await admin.PostBrowserJsonAsync($"/api/v1/admin/merchants/{review.Id}/approve", new MerchantReviewWrite(review.RowVersion))) Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        foreach (var path in new[] { $"stores/{store.Id}", $"stores/{store.Id}/logo", $"branches/{branch.Id}" }) { using var visible = await guest.GetAsync("/api/v1/" + path); Assert.Equal(HttpStatusCode.OK, visible.StatusCode); }
        using (var closedPlace = await guest.GetAsync($"/api/v1/branches/{branch.Id}")) Assert.False((await closedPlace.Content.ReadFromJsonAsync<PublicBranchDto>())!.Opening.OpenNow);
        using (var hidden = await admin.PostBrowserJsonAsync($"/api/v1/admin/stores/{store.Id}/hide", new StoreModerationWrite(store.RowVersion, "visibility review"))) { Assert.Equal(HttpStatusCode.OK, hidden.StatusCode); store = (await hidden.Content.ReadFromJsonAsync<StoreDto>())!; }
        using (var hiddenPlace = await guest.GetAsync($"/api/v1/branches/{branch.Id}")) Assert.Equal(HttpStatusCode.NotFound, hiddenPlace.StatusCode);
        using (var published = await admin.PostBrowserJsonAsync($"/api/v1/admin/stores/{store.Id}/publish", new StoreModerationWrite(store.RowVersion, "resolved"))) { Assert.Equal(HttpStatusCode.OK, published.StatusCode); store = (await published.Content.ReadFromJsonAsync<StoreDto>())!; }
        var previousActivity = store.ActivityId; var target = await StoreTestData.ActivityAsync(database); ActivityChangeDto request;
        using (var requested = await owner.PostBrowserJsonAsync($"/api/v1/merchants/stores/{store.Id}/activity-change-requests", new ActivityChangeWrite(target, store.RowVersion))) { Assert.Equal(HttpStatusCode.OK, requested.StatusCode); request = (await requested.Content.ReadFromJsonAsync<ActivityChangeDto>())!; }
        using (var current = await guest.GetAsync($"/api/v1/stores/{store.Id}")) Assert.Equal(previousActivity, (await current.Content.ReadFromJsonAsync<PublicStoreDto>())!.ActivityId);
        using (var accepted = await admin.PostBrowserJsonAsync($"/api/v1/admin/store-activity-changes/{request.Id}/approve", new ActivityChangeReviewWrite(request.RowVersion, request.StoreRowVersion))) Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using (var current = await guest.GetAsync($"/api/v1/stores/{store.Id}")) Assert.Equal(target, (await current.Content.ReadFromJsonAsync<PublicStoreDto>())!.ActivityId);
        using (var audit = await admin.GetAsync($"/api/v1/admin/stores/{store.Id}/audit?pageSize=100")) Assert.Contains((await audit.Content.ReadFromJsonAsync<MerchantPage<MerchantAuditDto>>())!.Items, value => value.Operation == "Store.ActivityChange.Approve" && value.ActorId == administrator.Id);
    }
}
